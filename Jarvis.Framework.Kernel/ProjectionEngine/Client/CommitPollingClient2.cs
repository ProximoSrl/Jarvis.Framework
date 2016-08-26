using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Support;
using Metrics;
using NEventStore;
using NEventStore.Client;
using NEventStore.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{

    /// <summary>
    /// Based on the new polling client of NEventStore.
    /// </summary>
    public class CommitPollingClient2 : ICommitPollingClient
    {
        private int _bufferSize = 4000;
        private readonly int _interval;
        readonly ICommitEnhancer _enhancer;
        private String _id;

        readonly ILogger _logger;

        public CommitPollingClientStatus Status { get; private set; }

        public Exception LastException { get; private set; }

        PollingClient2 _innerClient;

        CommitSequencer _sequencer;

        IPersistStreams _persistStream;

        public CommitPollingClient2(
            IPersistStreams persistStreams,
            ICommitEnhancer enhancer,
            String id,
            ILogger logger)
        {
            if (persistStreams == null)
            {
                throw new ArgumentNullException("persistStream");
            }
            _enhancer = enhancer;
            _consumers = new List<ITargetBlock<ICommit>>();
            _persistStream = persistStreams;
            _logger = logger;
            _id = id;
            Status = CommitPollingClientStatus.Stopped;
        }

        public void AddConsumer(Action<ICommit> consumerAction)
        {
            ExecutionDataflowBlockOptions consumerOptions = new ExecutionDataflowBlockOptions();
            consumerOptions.BoundedCapacity = _bufferSize;
            Action<ICommit> wrapperAction = commit =>
            {
                try
                {
                    consumerAction(commit);
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "CommitPollingClient {0}: Error during Commit consumer {1} ", _id, ex.Message);
                    StopPolling(true); //Stop and close everything on this poller.
                    LastException = ex;
                    throw;
                }
            };
            var actionBlock = new ActionBlock<ICommit>(wrapperAction, consumerOptions);

            _consumers.Add(actionBlock);
        }

        #region Dispatching

        private List<ITargetBlock<ICommit>> _consumers;

        #endregion

        #region polling

        private Int64 _checkpointTokenCurrent;

        private CancellationTokenSource _stopRequested = new CancellationTokenSource();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="checkpointTokenFrom"></param>
        /// <param name="intervalInMilliseconds"></param>
        /// <param name="checkpointTokenSequenced">When there is a rebuild we already know that commit until the
        /// rebuild checkpoint are already sequenced so there is no need to wait for them. All CheckpointToken
        /// less than this value are considered to be sequenced.</param>
        /// <param name="bufferSize"></param>
        /// <param name="pollerName"></param>
        public void StartAutomaticPolling(
          Int64 checkpointTokenFrom,
          Int32 intervalInMilliseconds,
          Int64 checkpointTokenSequenced,
          Int32 bufferSize = 4000,
          String pollerName = "CommitPollingClient")
        {
            Init(checkpointTokenFrom, intervalInMilliseconds,checkpointTokenSequenced, bufferSize, pollerName);
            _innerClient.StartFrom(checkpointTokenFrom);
            Status = CommitPollingClientStatus.Polling;
        }

        public void StartManualPolling(Int64 checkpointTokenFrom, Int32 intervalInMilliseconds, Int32 bufferSize = 4000, String pollerName = "CommitPollingClient")
        {
            Init(checkpointTokenFrom, intervalInMilliseconds, 0,bufferSize, pollerName);
            _innerClient.ConfigurePollingFunction();
        }

        private void Init(Int64 checkpointTokenFrom, int intervalInMilliseconds, Int64 checkpointTokenSequenced, int bufferSize, string pollerName)
        {
            _bufferSize = bufferSize;
            _checkpointTokenCurrent = checkpointTokenFrom;
            LastException = null;
            //prepare single poller thread.
            CreateTplChain();

            _sequencer = new CommitSequencer(DispatchCommit, _checkpointTokenCurrent, 2000);
            _innerClient = new PollingClient2(
                _persistStream,
                c =>
                {
                    if (c.CheckpointToken <= checkpointTokenSequenced)
                        return DispatchCommit(c);

                    return _sequencer.Handle(c);
                },
                intervalInMilliseconds);

            MetricsHelper.SetCommitPollingClientBufferSize(pollerName, () => GetClientBufferSize());
            //Set health check for polling
            HealthChecks.RegisterHealthCheck("Polling-" + pollerName, () =>
            {
                if (Status == CommitPollingClientStatus.Stopped)
                {
                    //poller is stopped, system healty
                    return HealthCheckResult.Healthy("Automatic polling stopped");
                }
                else if (Status == CommitPollingClientStatus.Faulted)
                {
                    //poller is stopped, system healty
                    var exception = (LastException != null ? LastException.ToString() : "");
                    exception = exception.Replace("{", "{{").Replace("}", "}}");
                    return HealthCheckResult.Unhealthy("Faulted (exception in consumer): " + exception);
                }
                var elapsed = DateTime.UtcNow - _innerClient.LastActivityTimestamp;
                if (elapsed.TotalMilliseconds > 5000)
                {
                    //more than 5 seconds without a poll, polling probably is stopped
                    return HealthCheckResult.Unhealthy(String.Format("poller stuck, last polling {0} ms ago", elapsed));
                }

                return HealthCheckResult.Healthy("Poller alive");
            });
        }


        public void StopPolling(Boolean setToFaulted = false)
        {
            _innerClient.Stop();
            CloseEverything();
            Status = setToFaulted ?
                CommitPollingClientStatus.Faulted :
                CommitPollingClientStatus.Stopped;
        }

        private void CloseEverything()
        {
            _stopRequested.Cancel();
            CloseTplChain();
        }


        /// <summary>
        /// Add a manual poll command, it will be executed from the single thread that
        /// read commits from stream.
        /// </summary>
        public void Poll()
        {
            //Add poll command if there are no other poll command in queuel
            _innerClient.PollNow();
        }

        private double GetClientBufferSize()
        {
            if (_buffer != null)
                return _buffer.Count;
            return 0;
        }

        private Int32 _postErrors = 0;

        private PollingClient2.HandlingResult DispatchCommit(ICommit commit)
        {
            try
            {
                if (_stopRequested.IsCancellationRequested)
                {
                    CloseTplChain();
                    return PollingClient2.HandlingResult.Stop;
                }

                _enhancer.Enhance(commit);
                var task = _buffer.SendAsync(commit);
                //wait till the commit is stored in tpl queue
                while (!task.Wait(2000))
                {
                    //maybe the mesh is full, but check for completion
                    if (_stopRequested.IsCancellationRequested)
                    {
                        _buffer.Complete();
                        return PollingClient2.HandlingResult.Stop;
                    }
                };
                //Check if block postponed the message, if for some
                //reason the message is postponed, exit from the polling
                if (!task.Result)
                {
                    if (_postErrors > 2)
                    {
                        //TPL is somewhat stuck
                        _logger.ErrorFormat("CommitPollingClient {0}: Unable to dispatch commit {1} into TPL chain, buffer block did not accept message", _id, commit.CheckpointToken);
                        _stopRequested.Cancel();
                    }
                    _postErrors++;
                    _logger.WarnFormat("CommitPollingClient {0}: TPL did not accept message for commit {1}", _id, commit.CheckpointToken);
                    Thread.Sleep(1000); //let some time to flush some messages.
                    return PollingClient2.HandlingResult.Retry;
                }
                _postErrors = 0;
                _checkpointTokenCurrent = commit.CheckpointToken;
                return PollingClient2.HandlingResult.MoveToNext;
            }
            catch (Exception ex)
            {
                return PollingClient2.HandlingResult.Stop;
            }
        }

        #endregion

        #region Tpl

        private BufferBlock<ICommit> _buffer;
        private ITargetBlock<ICommit> _broadcaster;
        private Boolean tplChainActive = false;

        private void CreateTplChain()
        {
            tplChainActive = true;
            DataflowBlockOptions bufferOptions = new DataflowBlockOptions();
            bufferOptions.BoundedCapacity = _bufferSize;
            _buffer = new BufferBlock<ICommit>(bufferOptions);

            ExecutionDataflowBlockOptions executionOption = new ExecutionDataflowBlockOptions();
            executionOption.BoundedCapacity = _bufferSize;

            _broadcaster = GuaranteedDeliveryBroadcastBlock.Create(_consumers, _id, 3000);

            _buffer.LinkTo(_broadcaster, new DataflowLinkOptions() { PropagateCompletion = true });
        }

        private void CloseTplChain()
        {
            //signal Tpl to complete buffer.
            _buffer.Complete();
            _broadcaster.Complete();
            tplChainActive = false;
        }

        #endregion

    }
}

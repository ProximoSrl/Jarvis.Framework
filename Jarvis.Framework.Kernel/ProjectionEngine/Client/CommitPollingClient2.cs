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

        readonly ICommitEnhancer _enhancer;
        private readonly String _id;

        readonly ILogger _logger;

        public CommitPollingClientStatus Status { get; private set; }

        public Exception LastException { get; private set; }

        PollingClient2 _innerClient;

        CommitSequencer _sequencer;
        readonly IPersistStreams _persistStream;

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

        public void AddConsumer(Func<ICommit, Task> consumerAction)
        {
            ExecutionDataflowBlockOptions consumerOptions = new ExecutionDataflowBlockOptions();
            consumerOptions.BoundedCapacity = _bufferSize;
            ConsumerActionWrapper wrapper = new ConsumerActionWrapper(consumerAction, this);
            var actionBlock = new ActionBlock<ICommit>((Func<ICommit, Task>) wrapper.Consume, consumerOptions);

            _consumers.Add(actionBlock);
        }

        #region Dispatching

        private readonly List<ITargetBlock<ICommit>> _consumers;

        private class ConsumerActionWrapper
        {
            private readonly Func<ICommit, Task> _consumerAction;
            private Boolean _active;
            private String _error;
  
            private readonly CommitPollingClient2 _owner;

            public ConsumerActionWrapper(Func<ICommit, Task> consumerAction, CommitPollingClient2 owner)
            {
                _consumerAction = consumerAction;
                _active = true;
                _owner = owner;
            }

            public string Error { get { return _error; } }
            public Boolean Active { get { return _active; } }

            public async Task Consume(ICommit commit)
            {
                if (Active == false) return; //consumer failed.
                try
                {
                    await _consumerAction(commit).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _owner._logger.ErrorFormat(ex, "CommitPollingClient {0}: Error during Commit consumer {1} ", _owner._id, ex.Message);
                    _active = false; //no more dispatch to this consumer.
                    _error = ex.ToString();
                    _owner.LastException = ex;
                    _owner.Status = CommitPollingClientStatus.Faulted;
                    //it does not throw, simply stops dispatching commit to this consumer that will be blocked
                }
            }
        }

        #endregion

        #region polling

        private Int64 _checkpointTokenCurrent;

        private readonly CancellationTokenSource _stopRequested = new CancellationTokenSource();

        /// <summary>
        /// Start automatic polling.
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
          Int32 bufferSize,
          String pollerName)
        {
            Init(checkpointTokenFrom, intervalInMilliseconds,checkpointTokenSequenced, bufferSize, pollerName);
            _innerClient.StartFrom(checkpointTokenFrom);
            Status = CommitPollingClientStatus.Polling;
        }

        public void StartManualPolling(Int64 checkpointTokenFrom, Int32 intervalInMilliseconds, Int32 bufferSize, String pollerName)
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

            RegisterHealthChecks(pollerName);
        }

        private void RegisterHealthChecks(string pollerName)
        {
            MetricsHelper.SetCommitPollingClientBufferSize(pollerName, () => GetClientBufferSize());
            //Set health check for polling
            HealthChecks.RegisterHealthCheck("Polling-" + pollerName, () =>
            {
                if (Status == CommitPollingClientStatus.Stopped)
                {
                    //poller is stopped, system healty
                    return HealthCheckResult.Healthy("Automatic polling stopped");
                }
                else if (Status == CommitPollingClientStatus.Faulted || LastException != null)
                {
                    //poller is stopped, system healty
                    var exceptionText = (LastException != null ? LastException.ToString() : "");
                    exceptionText = exceptionText.Replace("{", "{{").Replace("}", "}}");
                    return HealthCheckResult.Unhealthy("Faulted (exception in consumer): " + exceptionText);
                }
                var elapsed = DateTime.UtcNow - _innerClient.LastActivityTimestamp;
                if (elapsed.TotalMilliseconds > 5000)
                {
                    //more than 5 seconds without a poll, polling probably is stopped
                    return HealthCheckResult.Unhealthy(String.Format("poller stuck, last polling {0} ms ago", elapsed));
                }

                return HealthCheckResult.Healthy("Poller alive");
            });
            //Now register health check for the internal NES poller, to diagnose errors in polling.
            HealthChecks.RegisterHealthCheck("Polling internal errors: ", () =>
            {
                if (_innerClient != null && !String.IsNullOrEmpty(_innerClient.LastPollingError))
                {
                    return HealthCheckResult.Unhealthy($"Inner NES poller has error: {_innerClient.LastPollingError}");
                }
                return HealthCheckResult.Healthy("Inner NES Poller Ok");
            });
        }

        public void StopPolling(Boolean setToFaulted)
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

                if (commit.StreamId.StartsWith("system.empty"))
                {
                    _logger.DebugFormat("Found empty commit - {0}", commit.CheckpointToken);
                }
                else
                {
                    _enhancer.Enhance(commit);
                }

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
                _logger.ErrorFormat(ex, "Error in dispatching commit {0} - {1}", commit.CheckpointToken, ex.Message);
                return PollingClient2.HandlingResult.Stop;
            }
        }

        #endregion

        #region Tpl

        private BufferBlock<ICommit> _buffer;
        private ITargetBlock<ICommit> _broadcaster;

        private void CreateTplChain()
        {
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
        }

        #endregion

    }
}

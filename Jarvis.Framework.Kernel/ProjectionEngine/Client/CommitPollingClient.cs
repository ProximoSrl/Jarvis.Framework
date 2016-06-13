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

    public enum CommitPollingClientStatus
    {
        Stopped,
        Polling,
        Faulted
    }

    public interface ICommitPollingClient
    {
        void StartAutomaticPolling(
           String checkpointTokenFrom,
           Int32 intervalInMilliseconds,
           Int32 bufferSize,
           String pollerName);

        void Poll();

        void StartManualPolling(
           String checkpointTokenFrom,
           Int32 intervalInMilliseconds,
           Int32 bufferSize,
           String pollerName);

        void AddConsumer(Action<ICommit> consumerAction);

        void StopPolling(Boolean setToFaulted);
    }

    public class CommitPollingClient : ICommitPollingClient
    {
        private const string command_poll = "poll";
        private const string command_stop = "stop";
        private int _bufferSize = 4000;
        private readonly int _interval;
        readonly ICommitEnhancer _enhancer;
        private String _id;

        readonly ILogger _logger;
        private IPersistStreams _persistStreams;

        public CommitPollingClientStatus Status { get; private set; }

        public Exception LastException { get; private set; }

        public CommitPollingClient(
            IPersistStreams persistStreams,
            ICommitEnhancer enhancer,
            String id,
            ILogger logger)
        {
            if (persistStreams == null)
            {
                throw new ArgumentNullException("persistStreams");
            }
            _persistStreams = persistStreams;
            _enhancer = enhancer;
            _consumers = new List<ITargetBlock<ICommit>>();
            _commandList = new BlockingCollection<string>();
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

        private BlockingCollection<String> _commandList;

        private Thread _pollerThread;

        private System.Timers.Timer _pollerTimer;

        private String _checkpointTokenCurrent;

        private CancellationTokenSource _stopRequested = new CancellationTokenSource();

        private Int64 _lastActivityTickCount;

        public void StartAutomaticPolling(
            String checkpointTokenFrom,
            Int32 intervalInMilliseconds,
            Int32 bufferSize = 4000,
            String pollerName = "CommitPollingClient")
        {
            StartManualPolling(checkpointTokenFrom, intervalInMilliseconds, bufferSize, pollerName);
            _pollerTimer = new System.Timers.Timer(intervalInMilliseconds);
            _pollerTimer.Elapsed += TimerCallback;
            _pollerTimer.Start();

            //Add an immediate poll
            _commandList.Add(command_poll);

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
                var elapsed = Math.Abs(Environment.TickCount - _lastActivityTickCount);
                if (elapsed > 5000)
                {
                    //more than 5 seconds without a poll, polling probably is stopped
                    return HealthCheckResult.Unhealthy(String.Format("poller stuck, last polling {0} ms ago", elapsed));
                }

                return HealthCheckResult.Healthy("Poller alive");
            });

            Status = CommitPollingClientStatus.Polling;
        }

        public void StartManualPolling(
            String checkpointTokenFrom,
            Int32 intervalInMilliseconds,
            Int32 bufferSize = 4000,
            String pollerName = "CommitPollingClient")
        {
            _bufferSize = bufferSize;
            _checkpointTokenCurrent = checkpointTokenFrom;
            LastException = null;
            //prepare single poller thread.
            CreateTplChain();
            _pollerThread = new Thread(PollerFunc);
            _pollerThread.IsBackground = true;
            _pollerThread.Name = pollerName;
            _pollerThread.Start();
            MetricsHelper.SetCommitPollingClientBufferSize(pollerName, () => GetClientBufferSize());
        }

        public void StopPolling(Boolean setToFaulted = false)
        {
            if (_pollerTimer != null)
            {
                _pollerTimer.Stop();
                _pollerTimer.Dispose();
                _pollerTimer = null;
            }
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

        private void TimerCallback(object sender, System.Timers.ElapsedEventArgs e)
        {
            Poll();
        }

        /// <summary>
        /// Add a manual poll command, it will be executed from the single thread that
        /// read commits from stream.
        /// </summary>
        public void Poll()
        {
            //Add poll command if there are no other poll command in queuel
            if (!_commandList.Any(c => c.Equals(command_poll, StringComparison.OrdinalIgnoreCase)))
                _commandList.Add(command_poll);
        }

        private double GetClientBufferSize()
        {
            if (_buffer != null)
                return _buffer.Count;
            return 0;
        }

        private void PollerFunc(object obj)
        {
            foreach (var command in _commandList.GetConsumingEnumerable())
            {
                try
                {
                    switch (command)
                    {
                        case command_poll:
                            InternalPoll();
                            break;

                        case command_stop:
                            CloseTplChain();
                            return; //this will stop poller thread

                        default:
                            _logger.ErrorFormat("CommitPollingClient {0}: Unknown command {1}", _id, command);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "CommitPollingClient {0}: Error in executing command: {1}", _id, command);
                    throw;
                }
            }
        }

        private Int32 _postErrors = 0;
        private void InternalPoll()
        {
            _lastActivityTickCount = Environment.TickCount; //much faster than DateTime.now or UtcNow.
            if (_persistStreams.IsDisposed) return; //no need to poll, someone disposed NEventStore
            try
            {
                IEnumerable<ICommit> commits = _persistStreams.GetFrom(_checkpointTokenCurrent);
                foreach (var commit in commits)
                {
                    _lastActivityTickCount = Environment.TickCount; //much faster than DateTime.now or UtcNow.
                    if (_stopRequested.IsCancellationRequested)
                    {
                        CloseTplChain();
                        return;
                    }

                    _enhancer.Enhance(commit);
                    var task = _buffer.SendAsync(commit);
                    while (!task.Wait(2000))
                    {
                        //maybe the mesh is full, but check for completion
                        if (_stopRequested.IsCancellationRequested)
                        {
                            _buffer.Complete();
                            return;
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
                        return;
                    }
                    _postErrors = 0;
                    _checkpointTokenCurrent = commit.CheckpointToken;
                }
            }
            catch (Exception ex)
            {
                if (_stopRequested.IsCancellationRequested)
                    return;

                // These exceptions are expected to be transient, we can simply start a new poll immediately
                _logger.ErrorFormat(ex, "CommitPollingClient {0}: Error in polling client {1}", _id, ex.Message);

                Poll();
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

        #region Tpl private extensions

        private static class GuaranteedDeliveryBroadcastBlock
        {
            public static ITargetBlock<T> Create<T>(
                IEnumerable<ITargetBlock<T>> targets,
                String commitPollingClientId,
                Int32 boundedCapacity)
            {
                var options = new ExecutionDataflowBlockOptions();
                if (boundedCapacity > 0)
                {
                    options.BoundedCapacity = boundedCapacity;
                }
                var actionBlock = new ActionBlock<T>(
                    async item =>
                    {
                        foreach (var target in targets)
                        {
                            Int32 errorCount = 0;
                            Boolean result;
                            while (result = await target.SendAsync(item) == false)
                            {
                                Thread.Sleep(1000); //give some time to free some resource.
                                if (errorCount > 2)
                                {
                                    throw new Exception("GuaranteedDeliveryBroadcastBlock: Unable to send message to a target id " + commitPollingClientId);
                                }
                                errorCount++;
                            }
                        }
                    }, options);

                actionBlock.Completion.ContinueWith(t =>
                {
                    foreach (var target in targets)
                    {
                        target.Complete();
                    }
                });

                return actionBlock;
            }
        }

        #endregion
    }
}

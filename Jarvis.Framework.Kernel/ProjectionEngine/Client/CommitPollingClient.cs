using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Support;
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
    public interface IPollingClientConsumer
    {
        void Consume(ICommit commit);
    }

    public class CommitPollingClient
    {
        private const string command_poll = "poll";
        private const string command_stop = "stop";
        private int _bufferSize = 4000;
        private readonly int _interval;
        readonly ICommitEnhancer _enhancer;

        readonly ILogger _logger;
        private IPersistStreams _persistStreams;

        public CommitPollingClient(
            IPersistStreams persistStreams,
            ICommitEnhancer enhancer,
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
        }

        public void AddConsumer(IPollingClientConsumer consumer)
        {
            AddConsumer(commit => consumer.Consume(commit));
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
                    _logger.ErrorFormat(ex, "Error during Commit consumer {0} ", ex.Message);
                    CloseEverything();
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

        public void StartAutomaticPolling(
            String checkpointTokenFrom,
            Int32 intervalInMilliseconds,
            Int32 bufferSize = 4000,
            String pollerName = "CommitPollingClient")
        {
            StartManualPolling(checkpointTokenFrom, bufferSize, pollerName);
            _pollerTimer = new System.Timers.Timer(intervalInMilliseconds);
            _pollerTimer.Elapsed += TimerCallback;
            _pollerTimer.Start();

            //Add an immediate poll
            _commandList.Add(command_poll);
        }

        public void StartManualPolling(
            String checkpointTokenFrom,
            Int32 bufferSize = 4000,
            String pollerName = "CommitPollingClient")
        {
            _bufferSize = bufferSize;
            _checkpointTokenCurrent = checkpointTokenFrom;
            //prepare single poller thread.
            CreateTplChain();
            _pollerThread = new Thread(PollerFunc);
            _pollerThread.IsBackground = true;
            _pollerThread.Name = pollerName;
            _pollerThread.Start();
            MetricsHelper.SetCommitPollingClientBufferSize(pollerName, () => GetClientBufferSize());
        }

        public void StopPolling()
        {
            //TODO: Decide if we want to wait TPL
            _pollerTimer.Stop();
            _pollerTimer.Dispose();

            CloseEverything();
        }

        private void CloseEverything()
        {
            _stopRequested.Cancel();
            _commandList.Add(command_stop);
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
                            _logger.ErrorFormat("Unknown command {0}", command);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "Error in executing command: {0}", command);
                    throw;
                }
            }
        }

        private void InternalPoll()
        {
            if (_persistStreams.IsDisposed) return; //no need to poll, someone disposed NEventStore
            try
            {
                IEnumerable<ICommit> commits = _persistStreams.GetFrom(_checkpointTokenCurrent);
                foreach (var commit in commits)
                {
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
                    Debug.Assert(task.Result);
                    _checkpointTokenCurrent = commit.CheckpointToken;
                }
            }
            catch (Exception ex)
            {
                if (_stopRequested.IsCancellationRequested)
                    return;

                // These exceptions are expected to be transient, we can simply start a new poll immediately
                _logger.ErrorFormat(ex, "Error in polling client {0}", ex.Message);

                Poll();
            }
        }

        private void CloseTplChain()
        {
            //signal Tpl to complete buffer.
            _buffer.Complete();
        }

        #endregion

        #region Tpl

        private BufferBlock<ICommit> _buffer;
        //private TransformBlock<ICommit, ICommit> _enhancerBlock;
        private ITargetBlock<ICommit> _broadcaster;

        private void CreateTplChain()
        {
            DataflowBlockOptions bufferOptions = new DataflowBlockOptions();
            bufferOptions.BoundedCapacity = _bufferSize;
            _buffer = new BufferBlock<ICommit>(bufferOptions);

            ExecutionDataflowBlockOptions executionOption = new ExecutionDataflowBlockOptions();
            executionOption.BoundedCapacity = _bufferSize;
            //executionOption.MaxDegreeOfParallelism = 16;
            //_enhancerBlock = new TransformBlock<ICommit, ICommit>((Func<ICommit, ICommit>)Enhance, executionOption);

            _broadcaster = GuaranteedDeliveryBroadcastBlock.Create(_consumers, 3000);

            //_buffer.LinkTo(_enhancerBlock, new DataflowLinkOptions() { PropagateCompletion = true });
            //_enhancerBlock.LinkTo(_broadcaster, new DataflowLinkOptions() { PropagateCompletion = true });
            _buffer.LinkTo(_broadcaster, new DataflowLinkOptions() { PropagateCompletion = true });
        }

        private ICommit Enhance(ICommit arg)
        {
            _enhancer.Enhance(arg);
            return arg;
        }

        #endregion 

        #region Tpl private extensions

        private static class GuaranteedDeliveryBroadcastBlock
        {
            public static ITargetBlock<T> Create<T>(
                IEnumerable<ITargetBlock<T>> targets,
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
                            var result = await target.SendAsync(item);
                            Debug.Assert(result);
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

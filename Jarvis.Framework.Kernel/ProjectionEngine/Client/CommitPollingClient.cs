using Castle.Core.Logging;
using NEventStore;
using NEventStore.Client;
using NEventStore.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private readonly int _interval;
        readonly CommitEnhancer _enhancer;
        readonly bool _boost;
        readonly ILogger _logger;
        private IPersistStreams _persistStreams;

        public CommitPollingClient(
            IPersistStreams persistStreams,
            CommitEnhancer enhancer,
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
            ExecutionDataflowBlockOptions consumerOptions = new ExecutionDataflowBlockOptions();
            consumerOptions.BoundedCapacity = 1000;
            _consumers.Add(new ActionBlock<ICommit>(
                  commit =>
                  {
                      consumer.Consume(commit);
                  }, consumerOptions));
        }

        public void AddConsumer(Action<ICommit> consumerAction)
        {
            ExecutionDataflowBlockOptions consumerOptions = new ExecutionDataflowBlockOptions();
            consumerOptions.BoundedCapacity = 1000;
            _consumers.Add(new ActionBlock<ICommit>(consumerAction, consumerOptions));
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
            String pollerName = "CommitPollingClient")
        {
            StartManualPolling(checkpointTokenFrom, pollerName);
            _pollerTimer = new System.Timers.Timer(intervalInMilliseconds);
            _pollerTimer.Elapsed += TimerCallback;
            _pollerTimer.Start();

            //Add an immediate poll
            _commandList.Add(command_poll);
        }

        public void StartManualPolling(
            String checkpointTokenFrom,
            String pollerName = "CommitPollingClient")
        {
            _checkpointTokenCurrent = checkpointTokenFrom;
            //prepare single poller thread.
            CreateTplChain();
            _pollerThread = new Thread(PollerFunc);
            _pollerThread.IsBackground = true;
            _pollerThread.Name = pollerName;
            _pollerThread.Start();
        }

        public void StopPolling()
        {
            //TODO: Decide if we want to wait TPL
            _pollerTimer.Stop();
            _pollerTimer.Dispose();

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

                    while (!_buffer.SendAsync(commit).Wait(2000))
                    {
                        //maybe the mesh is full, but check for completion
                        if (_stopRequested.IsCancellationRequested)
                        {
                            _buffer.Complete();
                            return;
                        }
                    };
                    _checkpointTokenCurrent = commit.CheckpointToken;
                }
            }
            catch (Exception ex)
            {
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
        private TransformBlock<ICommit, ICommit> _enhancerBlock;
        private ITargetBlock<ICommit> _broadcaster;

        private void CreateTplChain()
        {
            DataflowBlockOptions bufferOptions = new DataflowBlockOptions();
            if (_boost)
            {
                bufferOptions.BoundedCapacity = 1000;
            }
            _buffer = new BufferBlock<ICommit>(bufferOptions);

            ExecutionDataflowBlockOptions executionOption = new ExecutionDataflowBlockOptions();
            executionOption.BoundedCapacity = 1000;
            executionOption.MaxDegreeOfParallelism = 16;
            _enhancerBlock = new TransformBlock<ICommit, ICommit>((Func<ICommit, ICommit>)Enhance, executionOption);

            ExecutionDataflowBlockOptions consumerOptions = new ExecutionDataflowBlockOptions();
            consumerOptions.BoundedCapacity = 1000;

            _broadcaster = GuaranteedDeliveryBroadcastBlock.Create(_consumers, 1000);

            _buffer.LinkTo(_enhancerBlock, new DataflowLinkOptions() { PropagateCompletion = true });
            _enhancerBlock.LinkTo(_broadcaster, new DataflowLinkOptions() { PropagateCompletion = true });
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
                List<ITargetBlock<T>> targets,
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
                            await target.SendAsync(item);
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

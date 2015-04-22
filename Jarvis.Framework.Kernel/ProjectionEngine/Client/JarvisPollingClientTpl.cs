using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using NEventStore;
using NEventStore.Client;
using NEventStore.Logging;
using NEventStore.Persistence;
using System.Threading.Tasks.Dataflow;
using System.Linq;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// Represents a client that poll the storage for latest commits.
    /// </summary>
    public class JarvisPollingClientTpl : ClientBase
    {
        private readonly int _interval;
        readonly CommitEnhancer _enhancer;
        readonly bool _boost;

        public JarvisPollingClientTpl(
            IPersistStreams persistStreams,
            int interval,
            CommitEnhancer enhancer,
            bool boost)
            : base(persistStreams)
        {
            if (persistStreams == null)
            {
                throw new ArgumentNullException("persistStreams");
            }
            if (interval <= 0)
            {
                throw new ArgumentException("interval");
            }
            _interval = interval;
            _enhancer = enhancer;
            _boost = boost;
        }

        /// <summary>
        /// Observe commits from the sepecified checkpoint token. If the token is null,
        /// all commits from the beginning will be observed.
        /// </summary>
        /// <param name="checkpointToken">The checkpoint token.</param>
        /// <returns>
        /// An <see cref="IObserveCommits" /> instance.
        /// </returns>
        public override IObserveCommits ObserveFrom(string checkpointToken = null)
        {
            return new PollingObserveCommitsTpl(PersistStreams, _interval, checkpointToken, _enhancer, _boost);
        }

        private class PollingObserveCommitsTpl : IObserveCommits
        {
            private ILog Logger = LogFactory.BuildLogger(typeof(PollingClient));

            private readonly IPersistStreams _persistStreams;
            private string _checkpointToken;
            readonly CommitEnhancer _enhancer;
            private readonly int _interval;

            private readonly CancellationTokenSource _stopRequested = new CancellationTokenSource();
            private TaskCompletionSource<Unit> _runningTaskCompletionSource;
            readonly bool _boost;
            private int _isPolling = 0;

            private BufferBlock<ICommit> _buffer;
            private TransformBlock<ICommit, ICommit> _enhancerBlock;
            private ITargetBlock<ICommit> _broadcaster;

            private ConcurrentDictionary<Guid, ITargetBlock<ICommit>> _subscribers;

            public PollingObserveCommitsTpl(
                IPersistStreams persistStreams,
                int interval,
                string checkpointToken,
                CommitEnhancer enhancer,
                bool boost)
            {
                _persistStreams = persistStreams;
                _checkpointToken = checkpointToken;
                _enhancer = enhancer;
                _interval = interval;
                _boost = boost;

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

                _subscribers = new ConcurrentDictionary<Guid, ITargetBlock<ICommit>>();

                _broadcaster = GuaranteedDeliveryBroadcastBlock.Create(_subscribers, 1000);

                _buffer.LinkTo(_enhancerBlock, new DataflowLinkOptions() { PropagateCompletion = true });
                _enhancerBlock.LinkTo(_broadcaster, new DataflowLinkOptions() { PropagateCompletion = true });
            }

            private ICommit Enhance(ICommit arg)
            {
                _enhancer.Enhance(arg);
                return arg;
            }

            public IDisposable Subscribe(IObserver<ICommit> observer)
            {
                ExecutionDataflowBlockOptions executionOption = new ExecutionDataflowBlockOptions();
                executionOption.BoundedCapacity = 1000;
                var subscriberAction = new ActionBlock<ICommit>(
                    c =>
                    {
                        observer.OnNext(c);
                    }, executionOption);

                Guid subscriptionId = Guid.NewGuid();
                _subscribers[subscriptionId] = subscriberAction;

                return new DisposableAction(() =>
                {
                    ITargetBlock<ICommit> subscriber;
                    if (_subscribers.TryRemove(subscriptionId, out subscriber)) 
                    {
                        subscriber.Complete();
                    } 
                });
            }

            public void Dispose()
            {
                _stopRequested.Cancel();
                _buffer.Complete();
                if (_runningTaskCompletionSource != null)
                {
                    _runningTaskCompletionSource.TrySetResult(new Unit());
                }
            }

            public Task Start()
            {
                if (_runningTaskCompletionSource != null)
                {
                    return _runningTaskCompletionSource.Task;
                }
                _runningTaskCompletionSource = new TaskCompletionSource<Unit>();
                PollLoop();
                return _runningTaskCompletionSource.Task;
            }

            public void PollNow()
            {
                DoPoll();
            }

            private void PollLoop()
            {
                if (_stopRequested.IsCancellationRequested ||
                    _persistStreams.IsDisposed)
                {
                    Dispose();
                    return;
                }
                TaskHelpers.Delay(_interval, _stopRequested.Token)
                                 .WhenCompleted(_ =>
                                 {
                                     DoPoll();
                                     PollLoop();
                                 }, _ => Dispose());
            }

            private Int32 skippedPoolCount = 0;

            private void DoPoll()
            {
                if (_persistStreams.IsDisposed) return; //no need to poll, someone disposed NEventStore wihout sh
                if (Interlocked.CompareExchange(ref _isPolling, 1, 0) == 0)
                {
                    Int32 exceptionCount = 0;
                    do
                    {
                        try
                        {
                            skippedPoolCount = 0;
                            IEnumerable<ICommit> commits = _persistStreams.GetFrom(_checkpointToken);
                            foreach (var commit in commits)
                            {
                                if (_stopRequested.IsCancellationRequested)
                                {
                                    _buffer.Complete();
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
                                _checkpointToken = commit.CheckpointToken;
                            }
                        }
                        catch (Exception ex)
                        {
                            // These exceptions are expected to be transient
                            Logger.Error(ex.ToString());
                            exceptionCount++;
                            if (_persistStreams.IsDisposed || exceptionCount > 5)
                            {
                                //eventstore is stopped or too many exception, need to exit poll cycle.
                                skippedPoolCount = 0;
                            }
                            else
                            {
                                //conceptually if we have an exception and we are in manual poll we
                                //are potentially skipping some commit so we need to reschedule a poll
                                Interlocked.Increment(ref skippedPoolCount);
                            }
                        }
                    } while (skippedPoolCount > 0);
                    Interlocked.Exchange(ref _isPolling, 0);
                }
                else
                {
                    Interlocked.Increment(ref skippedPoolCount);
                    Logger.Debug("Poll skipped, count " + skippedPoolCount);
                }
            }


        }

        private class DisposableAction : IDisposable
        {
            private Action _action;

            public DisposableAction(Action action)
            {
                _action = action;
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _action();
                }
            }

            public void Dispose()
            {
                Dispose(true);
            }
        }

        private static class GuaranteedDeliveryBroadcastBlock
        {
            public static ITargetBlock<T> Create<T>(ConcurrentDictionary<Guid, ITargetBlock<T>> targets, Int32 boundedCapacity)
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
                            await target.Value.SendAsync(item);
                        }
                    }, options);

                actionBlock.Completion.ContinueWith(t =>
                {
                    foreach (var target in targets)
                    {
                        target.Value.Complete();
                    }
                });

                return actionBlock;
            }
        }
    }


}

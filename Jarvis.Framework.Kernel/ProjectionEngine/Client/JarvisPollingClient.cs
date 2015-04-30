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

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// Represents a client that poll the storage for latest commits.
    /// </summary>
    public class JarvisPollingClient : ClientBase
    {
        private readonly int _interval;
        readonly ICommitEnhancer _enhancer;
        readonly bool _boost;
        private IConcurrentCheckpointTracker _tracker;
        private Int32 bufferSize = 1000;

        public JarvisPollingClient(IPersistStreams persistStreams, int interval, ICommitEnhancer enhancer, bool boost, IConcurrentCheckpointTracker tracker)
            : base(persistStreams)
        {
            if (persistStreams == null)
            {
                throw new ArgumentNullException("persistStreams");
            }
            if (interval <= 0)
            {
                throw new ArgumentException("interval");

                //                throw new ArgumentException(NEventStore.Messages.MustBeGreaterThanZero.FormatWith("interval"));
            }
            _interval = interval;
            _enhancer = enhancer;
            _boost = boost;
            _tracker = tracker;
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
            return new PollingObserveCommits(PersistStreams, _interval, checkpointToken, _enhancer, _boost, _tracker);
        }

        private class PollingObserveCommits : IObserveCommits
        {
            private ILog Logger = LogFactory.BuildLogger(typeof(PollingClient));

            private readonly IPersistStreams _persistStreams;
            private string _checkpointToken;
            readonly ICommitEnhancer _enhancer;
            private readonly int _interval;
            private readonly Subject<ICommit> _subject = new Subject<ICommit>();
            private readonly CancellationTokenSource _stopRequested = new CancellationTokenSource();
            private TaskCompletionSource<Unit> _runningTaskCompletionSource;
            readonly bool _boost;
            private int _isPolling = 0;
            private IConcurrentCheckpointTracker _tracker;

            public PollingObserveCommits(IPersistStreams persistStreams, int interval, string checkpointToken, ICommitEnhancer enhancer, bool boost, IConcurrentCheckpointTracker tracker)
            {
                _persistStreams = persistStreams;
                _checkpointToken = checkpointToken;
                _enhancer = enhancer;
                _interval = interval;
                _boost = boost;
                _tracker = tracker;
            }

            public IDisposable Subscribe(IObserver<ICommit> observer)
            {
                if (_boost)
                {
                    return _subject
                        .ObserveOn(ThreadPoolScheduler.Instance)  // thread in cui gira lo slot delle projection
                        //.SubscribeOn(NewThreadScheduler.Default) //  thread di lettura dei dati
                        .Subscribe(observer);
                }

                return _subject.Subscribe(observer);
            }

            public void Dispose()
            {
                _stopRequested.Cancel();
                _subject.Dispose();
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
                            Int64 i = 0;
                            foreach (var commit in commits)
                            {
                                if (_stopRequested.IsCancellationRequested)
                                {
                                    _subject.OnCompleted();
                                    return;
                                }

                                _enhancer.Enhance(commit);

                                _subject.OnNext(commit);
                                i++;
                                if (i % 1000 == 0)
                                {
                                    var minCheckpoint = _tracker.GetMinCheckpoint();
                                    var actualCheckpoint = Int64.Parse(commit.CheckpointToken);
                                    while (actualCheckpoint > minCheckpoint + 1000)
                                    {
                                        Thread.Sleep(500);
                                        minCheckpoint = _tracker.GetMinCheckpoint();
                                    }
                                }
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
    }
}

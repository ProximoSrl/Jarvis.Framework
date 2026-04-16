using Castle.Core.Logging;
using Jarvis.Framework.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    /// <summary>
    /// <para>
    /// Static coordinator that manages a single shared timer for all
    /// <see cref="AtomicMongoCollectionWrapper{TModel}"/> instances that use
    /// deferred version updates.
    /// </para>
    /// <para>
    /// Each wrapper registers a trigger callback when its deferred pipeline
    /// is first initialized. The shared timer fires periodically and invokes
    /// all registered triggers, causing partial batches to flush without
    /// waiting for the batch size threshold.
    /// </para>
    /// </summary>
    public static class DeferredUpdateVersionCoordinator
    {
        private static readonly ConcurrentDictionary<object, RegisteredPipeline> _registeredPipelines
            = new ConcurrentDictionary<object, RegisteredPipeline>();

        private static Timer _timer;
        private static readonly object _timerLock = new object();
        private static volatile bool _timerStarted;
        private static ILogger _logger = NullLogger.Instance;

        internal static void SetLogger(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Default drain timeout in milliseconds used by <see cref="FlushAllAsync"/>
        /// when the caller does not specify one.
        /// </summary>
        internal const int DefaultDrainTimeoutMs = 30_000;

        /// <summary>
        /// Register a deferred pipeline. The <paramref name="triggerBatch"/> action
        /// will be called by the shared timer to flush partial batches.
        /// </summary>
        /// <param name="key">Unique key identifying the pipeline instance (typically the wrapper itself).</param>
        /// <param name="triggerBatch">Action that triggers a partial batch flush (calls BatchBlock.TriggerBatch).</param>
        /// <param name="waitDrain">
        /// Async function that waits for all currently in-flight items to be processed,
        /// without completing (destroying) the pipeline. Receives a timeout in milliseconds.
        /// Called by <see cref="FlushAllAsync"/>.
        /// </param>
        /// <param name="completeAndWait">
        /// Async function that completes the pipeline and waits for all pending items to be processed.
        /// Called during shutdown via <see cref="StopAndFlushAllAsync"/>.
        /// </param>
        internal static void Register(object key, Action triggerBatch, Func<int, Task> waitDrain, Func<Task> completeAndWait)
        {
            _registeredPipelines[key] = new RegisteredPipeline(triggerBatch, waitDrain, completeAndWait);
            EnsureTimerStarted();
        }

        /// <summary>
        /// Unregister a pipeline. Called when a wrapper is no longer needed.
        /// </summary>
        internal static void Unregister(object key)
        {
            _registeredPipelines.TryRemove(key, out _);
        }

        /// <summary>
        /// Triggers all registered partial batches and waits for in-flight items to
        /// drain. The shared timer and registrations are <b>not</b> affected, so the
        /// pipelines remain usable after the call returns. Use this when you need a
        /// guaranteed flush point (e.g. projection engine stop) without tearing down
        /// the coordinator.
        /// </summary>
        /// <param name="drainTimeoutMs">
        /// Maximum time in milliseconds to wait for each pipeline to drain.
        /// Defaults to <see cref="DefaultDrainTimeoutMs"/> (30 seconds).
        /// </param>
        public static async Task FlushAllAsync(int drainTimeoutMs = DefaultDrainTimeoutMs)
        {
            var pipelines = _registeredPipelines.Values.ToList();

            if (pipelines.Count == 0)
            {
                return;
            }

            foreach (var pipeline in pipelines)
            {
                try
                {
                    pipeline.TriggerBatch();
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "Error triggering batch during flush");
                }
            }

            var waitTasks = new List<Task>();
            foreach (var pipeline in pipelines)
            {
                try
                {
                    waitTasks.Add(pipeline.WaitDrain(drainTimeoutMs));
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "Error waiting for pipeline drain during flush");
                }
            }

            await Task.WhenAll(waitTasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Stops the shared timer and clears all registrations without flushing.
        /// Call <see cref="FlushAllAsync"/> first if you need to drain pending items.
        /// This method is private and only invoked via reflection from tests.
        /// </summary>
        private static void Reset()
        {
            StopTimer();
            _registeredPipelines.Clear();
        }

        private static void EnsureTimerStarted()
        {
            if (_timerStarted)
            {
                return;
            }

            lock (_timerLock)
            {
                if (_timerStarted)
                {
                    return;
                }

                var intervalMs = JarvisFrameworkGlobalConfiguration.DeferredUpdateVersionFlushIntervalMs;
                _timer = new Timer(TimerCallback, null, intervalMs, intervalMs);
                _timerStarted = true;
            }
        }

        private static void StopTimer()
        {
            lock (_timerLock)
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
                _timerStarted = false;
            }
        }

        private static void TimerCallback(object state)
        {
            foreach (var kvp in _registeredPipelines)
            {
                try
                {
                    kvp.Value.TriggerBatch();
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "Error in deferred UpdateVersion timer callback for key {0}", kvp.Key);
                }
            }
        }

        private sealed class RegisteredPipeline
        {
            public Action TriggerBatch { get; }
            public Func<int, Task> WaitDrain { get; }
            public Func<Task> CompleteAndWait { get; }

            public RegisteredPipeline(Action triggerBatch, Func<int, Task> waitDrain, Func<Task> completeAndWait)
            {
                TriggerBatch = triggerBatch;
                WaitDrain = waitDrain;
                CompleteAndWait = completeAndWait;
            }
        }
    }
}

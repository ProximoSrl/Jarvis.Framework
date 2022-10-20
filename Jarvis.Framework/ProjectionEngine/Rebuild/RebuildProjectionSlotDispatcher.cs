using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.HealthCheck;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    /// <summary>
    /// Helps dispatching events to projection in a slot.
    /// </summary>
    internal class RebuildProjectionSlotDispatcher
    {
        [DllImport("KERNEL32")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        public String SlotName { get; private set; }
        private readonly ILogger _logger;
        private readonly ProjectionEngineConfig _config;
        private readonly IEnumerable<IProjection> _projections;
        private readonly ProjectionMetrics _metrics;

        /// <summary>
        /// Needed because if an events come greater than this value, we do not
        /// need to dispatch the event because it is an event that was not 
        /// dispatched even for standard projection.
        /// </summary>
        private readonly Int64 _maxCheckpointDispatched;
        private Int64 _lastCheckpointRebuilded;

        /// <summary>
        /// TODO: We should pass a dictionary where we have the last dispatched
        /// checkpoint for EACH projection. For this first version we can simply 
        /// use the maximum dispatched slot because all the projection in a slot
        /// can differs only by one element.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="slotName">Each instance of this class dispatch events for a single slot.</param>
        /// <param name="config">Needed only to know tentant id.</param>
        /// <param name="projections">All projections for the slot.</param>
        /// <param name="lastCheckpointDispatched"></param>
        public RebuildProjectionSlotDispatcher(
            ILogger logger,
            String slotName,
            ProjectionEngineConfig config,
            IEnumerable<IProjection> projections,
            Int64 lastCheckpointDispatched)
        {
            SlotName = slotName;
            _logger = logger;
            _config = config;
            _projections = projections;
            _metrics = new ProjectionMetrics(projections);
            _maxCheckpointDispatched = lastCheckpointDispatched;
            _lastCheckpointRebuilded = 0;
        }

        public Int64 LastCheckpointDispatched
        {
            get { return _maxCheckpointDispatched; }
        }

        public IEnumerable<IProjection> Projections
        {
            get { return _projections; }
        }

        public double CheckpointToDispatch { get { return LastCheckpointDispatched - _lastCheckpointRebuilded; } }

        public bool Finished { get; private set; }

        internal async Task DispatchEventAsync(UnwindedDomainEvent unwindedEvent)
        {
            if (unwindedEvent == UnwindedDomainEvent.LastEvent)
            {
                Finished = true;
                _lastCheckpointRebuilded = LastCheckpointDispatched; //Set to zero metrics, we dispatched everything.
                return;
            }

            var chkpoint = unwindedEvent.CheckpointToken;
            if (chkpoint > LastCheckpointDispatched)
            {
                if (_logger.IsDebugEnabled)
                {
                    _logger.DebugFormat("Discharded event {0} commit {1} because last checkpoint dispatched for slot {2} is {3}.", unwindedEvent.CommitId, unwindedEvent.CheckpointToken, SlotName, _maxCheckpointDispatched);
                }

                return;
            }

            Interlocked.Increment(ref RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);
            TenantContext.Enter(_config.TenantId);

            try
            {
                string eventName = unwindedEvent.EventType;
                foreach (var projection in _projections)
                {
                    var cname = projection.Info.CommonName;
                    long elapsedticks;
                    try
                    {
                        QueryPerformanceCounter(out long ticks1);
                        await projection.HandleAsync(unwindedEvent.GetEvent(), true).ConfigureAwait(false);
                        QueryPerformanceCounter(out long ticks2);
                        elapsedticks = ticks2 - ticks1;
                        JarvisFrameworkKernelMetricsHelper.IncrementProjectionCounterRebuild(cname, SlotName, eventName, elapsedticks);
                    }
                    catch (Exception ex)
                    {
                        _logger.FatalFormat(ex, "[Slot: {3} Projection: {4}] Failed checkpoint: {0} StreamId: {1} Event Name: {2}",
                            unwindedEvent.CheckpointToken,
                            unwindedEvent.PartitionId,
                            eventName,
                            SlotName,
                            cname
                        );
                        JarvisFrameworkHealthChecks.RegisterHealthCheck($"RebuildDispatcher, slot {SlotName} - FailedCheckpoint {unwindedEvent.CheckpointToken}", () =>
                            JarvisFrameworkHealthCheckResult.Unhealthy(ex)
                        );
                        throw;
                    }

                    _metrics.Inc(cname, eventName, elapsedticks);

                    if (_logger.IsDebugEnabled)
                    {
                        _logger.DebugFormat("[{3}] [{4}] Handled checkpoint {0}: {1} > {2}",
                            unwindedEvent.CheckpointToken,
                            unwindedEvent.PartitionId,
                            eventName,
                            SlotName,
                            cname
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Error dispathing commit id: {0}\nMessage: {1}\nError: {2}",
                    unwindedEvent.CheckpointToken, unwindedEvent.Event, ex.Message);
                JarvisFrameworkHealthChecks.RegisterHealthCheck($"RebuildDispatcher, slot {SlotName} - GeneralError", () =>
                    JarvisFrameworkHealthCheckResult.Unhealthy(ex)
                );
                throw;
            }
            _lastCheckpointRebuilded = chkpoint;
            JarvisFrameworkKernelMetricsHelper.MarkEventInRebuildDispatchedCount(SlotName, 1);
            Interlocked.Decrement(ref RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);
        }

        internal async Task DispatchStartEventAsync(DomainEvent domainEvent)
        {
            if (domainEvent == LastEventMarker.Instance)
            {
                Finished = true;
                _lastCheckpointRebuilded = LastCheckpointDispatched; //Set to zero metrics, we dispatched everything.
                return;
            }

            var chkpoint = domainEvent.CheckpointToken;
            if (chkpoint > LastCheckpointDispatched)
            {
                if (_logger.IsDebugEnabled)
                {
                    _logger.DebugFormat("Discharded event {0} commit {1} because last checkpoint dispatched for slot {2} is {3}.", domainEvent.CommitId, domainEvent.CheckpointToken, SlotName, _maxCheckpointDispatched);
                }

                return;
            }

            Interlocked.Increment(ref RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);
            TenantContext.Enter(_config.TenantId);

            try
            {
                string eventName = domainEvent.GetType().Name;
                foreach (var projection in _projections)
                {
                    var cname = projection.Info.CommonName;
                    long elapsedticks;
                    try
                    {
                        QueryPerformanceCounter(out long ticks1);
                        await projection.HandleAsync(domainEvent, true).ConfigureAwait(false);
                        QueryPerformanceCounter(out long ticks2);
                        elapsedticks = ticks2 - ticks1;
                        JarvisFrameworkKernelMetricsHelper.IncrementProjectionCounterRebuild(cname, SlotName, eventName, elapsedticks);
                    }
                    catch (Exception ex)
                    {
                        _logger.FatalFormat(ex, "[Slot: {3} Projection: {4}] Failed checkpoint: {0} StreamId: {1} Event Name: {2}",
                            domainEvent.CheckpointToken,
                            domainEvent.AggregateId,
                            eventName,
                            SlotName,
                            cname
                        );
                        JarvisFrameworkHealthChecks.RegisterHealthCheck($"RebuildDispatcher, slot {SlotName} - FailedCheckpoint {domainEvent.CheckpointToken}", () =>
                            JarvisFrameworkHealthCheckResult.Unhealthy(ex)
                        );
                        throw;
                    }

                    _metrics.Inc(cname, eventName, elapsedticks);

                    if (_logger.IsDebugEnabled)
                    {
                        _logger.DebugFormat("[{3}] [{4}] Handled checkpoint {0}: {1} > {2}",
                            domainEvent.CheckpointToken,
                            domainEvent.AggregateId,
                            eventName,
                            SlotName,
                            cname
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Error dispathing commit id: {0}\nMessage: {1}\nError: {2}",
                    domainEvent.CheckpointToken, domainEvent, ex.Message);
                JarvisFrameworkHealthChecks.RegisterHealthCheck($"RebuildDispatcher, slot {SlotName} - GeneralError", () =>
                    JarvisFrameworkHealthCheckResult.Unhealthy(ex)
                );
                throw;
            }
            _lastCheckpointRebuilded = chkpoint;
            JarvisFrameworkKernelMetricsHelper.MarkEventInRebuildDispatchedCount(SlotName, 1);
            Interlocked.Decrement(ref RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);
        }
    }
}

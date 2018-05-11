using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Support;
using Metrics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    internal class RebuildProjectionSlotDispatcher
    {
        [DllImport("KERNEL32")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        public String SlotName { get; private set; }
        private readonly ILogger _logger;
        private readonly ProjectionEngineConfig _config;
        private readonly IEnumerable<IProjection> _projections;
        private readonly ProjectionMetrics _metrics;
        private readonly Int64 _maxCheckpointDispatched;
        private Int64 _lastCheckpointRebuilded;
        private readonly ILoggerThreadContextManager _loggerThreadContextManager;

        /// <summary>
        /// TODO: We should pass a dictionary where we have the last dispatched
        /// checkpoint for EACH projection.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="slotName"></param>
        /// <param name="config"></param>
        /// <param name="projections"></param>
        /// <param name="lastCheckpointDispatched"></param>
        /// <param name="loggerThreadContextManager"></param>
        public RebuildProjectionSlotDispatcher(
            ILogger logger,
            String slotName,
            ProjectionEngineConfig config,
            IEnumerable<IProjection> projections,
            Int64 lastCheckpointDispatched,
            ILoggerThreadContextManager loggerThreadContextManager)
        {
            SlotName = slotName;
            _logger = logger;
            _config = config;
            _projections = projections;
            _metrics = new ProjectionMetrics(projections);
            _maxCheckpointDispatched = lastCheckpointDispatched;
            _lastCheckpointRebuilded = 0;
            _loggerThreadContextManager = loggerThreadContextManager;
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
                if (_logger.IsDebugEnabled) _logger.DebugFormat("Discharded event {0} commit {1} because last checkpoint dispatched for slot {2} is {3}.", unwindedEvent.CommitId, unwindedEvent.CheckpointToken, SlotName, _maxCheckpointDispatched);
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
                        KernelMetricsHelper.IncrementProjectionCounterRebuild(cname, SlotName, eventName, elapsedticks);
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
                        HealthChecks.RegisterHealthCheck($"RebuildDispatcher, slot {SlotName} - FailedCheckpoint {unwindedEvent.CheckpointToken}", () =>
                            HealthCheckResult.Unhealthy(ex)
                        );
                        throw;
                    }

                    _metrics.Inc(cname, eventName, elapsedticks);

                    if (_logger.IsDebugEnabled)
                        _logger.DebugFormat("[{3}] [{4}] Handled checkpoint {0}: {1} > {2}",
                            unwindedEvent.CheckpointToken,
                            unwindedEvent.PartitionId,
                            eventName,
                            SlotName,
                            cname
                        );
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Error dispathing commit id: {0}\nMessage: {1}\nError: {2}",
                    unwindedEvent.CheckpointToken, unwindedEvent.Event, ex.Message);
                HealthChecks.RegisterHealthCheck($"RebuildDispatcher, slot {SlotName} - GeneralError", () =>
                    HealthCheckResult.Unhealthy(ex)
                );
                throw;
            }
            _lastCheckpointRebuilded = chkpoint;
            KernelMetricsHelper.MarkEventInRebuildDispatchedCount(SlotName, 1);
            Interlocked.Decrement(ref RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);
        }
    }

    public class RebuildStatus
    {
        private int _bucketActiveCount = 0;
        public int BucketActiveCount
        {
            get { return _bucketActiveCount; }
        }

        public List<BucketSummaryStatus> SummaryStatus { get; set; }

        public Boolean Done { get { return BucketActiveCount == 0; } }

        public RebuildStatus()
        {
            SummaryStatus = new List<BucketSummaryStatus>();
        }

        public void AddBucket()
        {
            Interlocked.Increment(ref _bucketActiveCount);
        }

        public void BucketDone(
            String bucketDescription,
            Int64 eventsDispatched,
            Int64 durationInMilliseconds,
            Int64 totalEventsToDispatchWithoutFiltering)
        {
            Interlocked.Decrement(ref _bucketActiveCount);
            SummaryStatus.Add(new BucketSummaryStatus()
            {
                BucketDescription = bucketDescription,
                EventsDispatched = eventsDispatched,
                DurationInMilliseconds = durationInMilliseconds,
                TotalEventsToDispatchWithoutFiltering = totalEventsToDispatchWithoutFiltering,
            });
        }

        public class BucketSummaryStatus
        {
            public String BucketDescription { get; set; }

            public Int64 TotalEventsToDispatchWithoutFiltering { get; set; }

            public Int64 EventsDispatched { get; set; }

            public Int64 DurationInMilliseconds { get; set; }
        }
    }
}

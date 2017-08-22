using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Events;
using NEventStore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    internal class RebuildProjectionSlotDispatcher
    {
        public String SlotName { get; private set; }
        private IExtendedLogger _logger;
        private ProjectionEngineConfig _config;
        private IEnumerable<IProjection> _projections;
        private ProjectionMetrics _metrics;
        private IConcurrentCheckpointTracker _checkpointTracker;
        private Int64 _maxCheckpointDispatched;
        private Int64 _lastCheckpointRebuilded;

        public RebuildProjectionSlotDispatcher(
            IExtendedLogger logger,
            String slotName,
            ProjectionEngineConfig config,
            IEnumerable<IProjection> projections,
            IConcurrentCheckpointTracker checkpointTracker,
            Int64 lastCheckpointDispatched)
        {
            SlotName = slotName;
            _logger = logger;
            _config = config;
            _projections = projections;
            _metrics = new ProjectionMetrics(projections);
            _checkpointTracker = checkpointTracker;
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

        public bool Finished { get { return _lastCheckpointRebuilded == LastCheckpointDispatched; } }

        private void ClearLoggerThreadPropertiesForEventDispatchLoop()
        {
            if (_logger.IsDebugEnabled) _logger.ThreadProperties["evType"] = null;
            if (_logger.IsDebugEnabled) _logger.ThreadProperties["evMsId"] = null;
            if (_logger.IsDebugEnabled) _logger.ThreadProperties["evCheckpointToken"] = null;
            if (_logger.IsDebugEnabled) _logger.ThreadProperties["prj"] = null;
        }

        internal async Task DispatchEventAsync(DomainEvent evt)
        {
            var chkpoint = evt.CheckpointToken;
            if (chkpoint > LastCheckpointDispatched)
            {
                if (_logger.IsDebugEnabled) _logger.DebugFormat("Discharded event {0} commit {1} because last checkpoint dispatched for slot {2} is {3}.", evt.CommitId, evt.CheckpointToken, SlotName, _maxCheckpointDispatched);
                return;
            }

            Interlocked.Increment(ref RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);

            //Console.WriteLine("[{0:00}] - Slot {1}", Thread.CurrentThread.ManagedThreadId, slotName);
            if (_logger.IsDebugEnabled)
            {
                _logger.ThreadProperties["commit"] = evt.CommitId;
                _logger.DebugFormat("Dispatching checkpoit {0} on tenant {1}", evt.CheckpointToken, _config.TenantId);
            }

            TenantContext.Enter(_config.TenantId);

            try
            {
                if (_logger.IsDebugEnabled)
                {
                    _logger.ThreadProperties["evType"] = evt.GetType().Name;
                    _logger.ThreadProperties["evMsId"] = evt.MessageId;
                    _logger.ThreadProperties["evCheckpointToken"] = evt.CheckpointToken;
                }
                string eventName = evt.GetType().Name;
                foreach (var projection in _projections)
                {
                    var cname = projection.Info.CommonName;
                    if (_logger.IsDebugEnabled) _logger.ThreadProperties["prj"] = cname;

                    long ticks = 0;

                    try
                    {
                        //pay attention, stopwatch consumes time.
                        var sw = new Stopwatch();
                        sw.Start();
                        await projection.HandleAsync(evt, true).ConfigureAwait(false);
                        sw.Stop();
                        ticks = sw.ElapsedTicks;
                        MetricsHelper.IncrementProjectionCounterRebuild(cname, SlotName, eventName, ticks);
                    }
                    catch (Exception ex)
                    {
                        _logger.FatalFormat(ex, "[Slot: {3} Projection: {4}] Failed checkpoint: {0} StreamId: {1} Event Name: {2}",
                            evt.CheckpointToken,
                            evt.AggregateId,
                            eventName,
                            SlotName,
                            cname
                        );
                        throw;
                    }

                    _metrics.Inc(cname, eventName, ticks);

                    if (_logger.IsDebugEnabled)
                        _logger.DebugFormat("[{3}] [{4}] Handled checkpoint {0}: {1} > {2}",
                            evt.CheckpointToken,
                            evt.AggregateId,
                            eventName,
                            SlotName,
                            cname
                        );

                    if (_logger.IsDebugEnabled) _logger.ThreadProperties["prj"] = null;
                }

                ClearLoggerThreadPropertiesForEventDispatchLoop();
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Error dispathing commit id: {0}\nMessage: {1}\nError: {2}",
                    evt.CheckpointToken, evt, ex.Message);
                ClearLoggerThreadPropertiesForEventDispatchLoop();
                throw;
            }
            _lastCheckpointRebuilded = chkpoint;
            if (_logger.IsDebugEnabled) _logger.ThreadProperties["commit"] = null;
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

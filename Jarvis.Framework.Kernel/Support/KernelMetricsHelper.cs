using Jarvis.Framework.Kernel.MultitenantSupport;
using Metrics;
using System;
using System.Collections.Generic;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Engine;

namespace Jarvis.Framework.Kernel.Support
{
    /// <summary>
    /// Class to centralize metrics based on Metrics.NET
    /// </summary>
    public static class KernelMetricsHelper
    {
        private const String _checkpointToDispatchRebuildGaugeName ="checkpoint-to-dispatch";

        private const String _commitPollingClientBufferSizeGaugeName = "polling-client-buffer-size";

        /// <summary>
        /// Tells me how many concurrent commits we are dispatching.
        /// </summary>
        private const String _projectionEngineCurrentDispatchCount = "N° actual concurrent commit dispatch";

        public static void SetProjectionEngineCurrentDispatchCount(Func<double> valueProvider)
        {
            Metric.Gauge(_projectionEngineCurrentDispatchCount, valueProvider, Unit.Custom(EventStoreFactory.PartitionCollectionName));
        }

        public static void SetCheckpointCountToDispatch(String slotName, Func<double> valueProvider)
        {
            //These stats are valid only during a rebuild and not during standard working.
            if (!RebuildSettings.ShouldRebuild)
                return;

            String gaugeName;
            if (!String.IsNullOrEmpty(slotName))
            {
                gaugeName = _checkpointToDispatchRebuildGaugeName + "-" + slotName;
            }
            else
            {
                gaugeName = _checkpointToDispatchRebuildGaugeName;
            }
            if (TenantContext.CurrentTenantId != null)
            {
                gaugeName = "t[" + TenantContext.CurrentTenantId + "]" + gaugeName;
            }
            Metric.Gauge(gaugeName, valueProvider, Unit.Items);
        }

        public static void SetCommitPollingClientBufferSize(String pollerName, Func<double> valueProvider)
        {
            String gaugeName;
            if (!String.IsNullOrEmpty(pollerName))
            {
                gaugeName = _commitPollingClientBufferSizeGaugeName + "-" + pollerName;
            }
            else
            {
                gaugeName = _commitPollingClientBufferSizeGaugeName;
            }
            if (TenantContext.CurrentTenantId != null)
            {
                gaugeName = "t[" + TenantContext.CurrentTenantId + "]" + gaugeName;
            }
            Metric.Gauge(gaugeName, valueProvider, Unit.Items);
        }

        private static readonly Dictionary<string, Meter> CommitDispatchedBySlot = new Dictionary<string, Meter>();
        private static readonly Dictionary<string, Meter> RebuildCommitDispatchedBySlot = new Dictionary<string, Meter>();
        private static readonly Dictionary<string, Meter> RebuildEventDispatchedByBucket = new Dictionary<string, Meter>();

        public static void CreateMeterForDispatcherCountSlot(String slotName)
        {
            if (!CommitDispatchedBySlot.ContainsKey(slotName))
            {
                var meter = Metric.Meter("projection-chunk-dispatched-" + slotName, Unit.Items);
                CommitDispatchedBySlot[slotName] = meter;
            }
        }

        public static void CreateMeterForRebuildDispatcherBuffer(string slotName, Func<Double> getBufferDispatchCount)
        {
            if (!RebuildCommitDispatchedBySlot.ContainsKey(slotName))
            {
                var meter = Metric.Meter("projection-rebuild-event-dispatched-" + slotName, Unit.Items);
                RebuildCommitDispatchedBySlot[slotName] = meter;
            }
            Metric.Gauge("rebuild-slot-input-buffer-" + slotName, getBufferDispatchCount, Unit.Items);
        }

        public static void CreateGaugeForRebuildBucketDBroadcasterBuffer(string bucketKey, Func<Double> provider)
        {
            Metric.Gauge("rebuild-buffer-broadcaster-" + bucketKey, provider, Unit.Items);
        }

        public static void CreateGaugeForRebuildFirstBuffer(string bucketKey, Func<Double> provider)
        {
            Metric.Gauge("rebuild-firstbuffer-" + bucketKey, provider, Unit.Items);
        }

        public static void CreateMeterForRebuildEventCompleted(string bucketKey)
        {
            if (!RebuildEventDispatchedByBucket.ContainsKey(bucketKey))
            {
                var meter = Metric.Meter("rebuild-event-processed-" + bucketKey, Unit.Items);
                CommitDispatchedBySlot[bucketKey] = meter;
            }
        }

        public static void MarkCommitDispatchedCount(String slotName, Int32 count)
        {
            CommitDispatchedBySlot[slotName].Mark(count);
			projectionDispatchMeter.Mark(count);
		}

        public static void MarkEventInRebuildDispatchedCount(String slotName, Int32 count)
        {
            RebuildCommitDispatchedBySlot[slotName].Mark(count);
            projectionRebuildDispatchMeter.Mark(count);
        }

        public static void MarkRebuildEventProcessedCount(String bucketName, Int32 count)
        {
            RebuildEventDispatchedByBucket[bucketName].Mark(count);
        }

        private static readonly Counter projectionCounter = Metric.Counter("prj-time", Unit.Custom("ticks"));
        private static readonly Counter projectionSlotCounter = Metric.Counter("prj-slot-time", Unit.Custom("ticks"));
        private static readonly Counter projectionEventCounter = Metric.Counter("prj-event-time", Unit.Custom("ticks"));
		private static readonly Counter projectionSlowEventCounter = Metric.Counter("prj-slow-event", Unit.Custom("ms"));

		private static readonly Counter projectionCounterRebuild = Metric.Counter("prj-time-rebuild", Unit.Custom("ticks"));
        private static readonly Counter projectionSlotCounterRebuild = Metric.Counter("prj-slot-time-rebuild", Unit.Custom("ticks"));
        private static readonly Counter projectionEventCounterRebuild = Metric.Counter("prj-event-time-rebuild", Unit.Custom("ticks"));

		private static readonly Meter projectionDispatchMeter = Metric.Meter("projection-chunk-dispatched-TOTAL", Unit.Items, TimeUnit.Seconds);

        /// <summary>
        /// During rebuild, this is the count of ALL Slot dispatched event, each event is counted multiple times.
        /// </summary>
        private static readonly Meter projectionRebuildDispatchMeter = Metric.Meter("projection-rebuild-event-dispatched-TOTAL", Unit.Items, TimeUnit.Seconds);

        public static void IncrementProjectionCounter(String projectionName, String slotName, String eventName, Int64 ticks, Int64 milliseconds)
        {
            projectionCounter.Increment(projectionName, ticks);
            projectionSlotCounter.Increment(slotName, ticks);
            projectionEventCounter.Increment(eventName, ticks);
			if (milliseconds > 50)
			{
				projectionSlowEventCounter.Increment($"{projectionName}/{eventName}", milliseconds);
			}
		}

        public static void IncrementProjectionCounterRebuild(String projectionName, String slotName, String eventName, Int64 milliseconds)
        {
            projectionCounterRebuild.Increment(projectionName, milliseconds);
            projectionSlotCounterRebuild.Increment(slotName, milliseconds);
            projectionEventCounterRebuild.Increment(eventName, milliseconds);
        }
    }
}
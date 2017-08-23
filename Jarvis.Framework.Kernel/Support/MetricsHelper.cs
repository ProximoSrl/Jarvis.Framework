using Jarvis.Framework.Kernel.MultitenantSupport;
using Metrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Kernel.Engine;

namespace Jarvis.Framework.Kernel.Support
{
    /// <summary>
    /// Class to centralize metrics based on Metrics.NET
    /// </summary>
    public static class MetricsHelper
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

        private static readonly Dictionary<string, Meter> CommitDispatchIndex = new Dictionary<string, Meter>();

        public static void CreateMeterForDispatcherCountSlot(String slotName)
        {
            if (!CommitDispatchIndex.ContainsKey(slotName))
            {
                var meter = Metric.Meter("commit-dispatched-" + slotName, Unit.Items, TimeUnit.Seconds);
                CommitDispatchIndex[slotName] = meter;
            }
        }

        public static void MarkCommitDispatchedCount(String slotName, Int32 count)
        {
            CommitDispatchIndex[slotName].Mark(count);
        }

        private static readonly Counter projectionCounter = Metric.Counter("prj-time", Unit.Custom("ticks"));
        private static readonly Counter projectionSlotCounter = Metric.Counter("prj-slot-time", Unit.Custom("ticks"));
        private static readonly Counter projectionEventCounter = Metric.Counter("prj-event-time", Unit.Custom("ticks"));

        private static readonly Counter projectionCounterRebuild = Metric.Counter("prj-time-rebuild", Unit.Custom("ticks"));
        private static readonly Counter projectionSlotCounterRebuild = Metric.Counter("prj-slot-time-rebuild", Unit.Custom("ticks"));
        private static readonly Counter projectionEventCounterRebuild = Metric.Counter("prj-event-time-rebuild", Unit.Custom("ticks"));

        public static void IncrementProjectionCounter(String projectionName, String slotName, String eventName, Int64 milliseconds)
        {
            projectionCounter.Increment(projectionName, milliseconds);
            projectionSlotCounter.Increment(slotName, milliseconds);
            projectionEventCounter.Increment(eventName, milliseconds);
        }

        public static void IncrementProjectionCounterRebuild(String projectionName, String slotName, String eventName, Int64 milliseconds)
        {
            projectionCounterRebuild.Increment(projectionName, milliseconds);
            projectionSlotCounterRebuild.Increment(slotName, milliseconds);
            projectionEventCounterRebuild.Increment(eventName, milliseconds);
        }

        private static readonly Counter ConcurrencyExceptions = Metric.Counter("Concurrency Exceptions", Unit.Events);
        private static readonly Counter DomainExceptions = Metric.Counter("Domain Exceptions", Unit.Events);

        public static void MarkConcurrencyException()
        {
            ConcurrencyExceptions.Increment();
        }

        public static void MarkDomainException()
        {
            DomainExceptions.Increment();
        }

        public static readonly Timer CommandTimer = Metric.Timer("Commands Execution", Unit.Commands);
        public static readonly Counter CommandCounter = Metric.Counter("CommandsDuration", Unit.Custom("ms"));
    }
}
using Jarvis.Framework.Kernel.MultitenantSupport;
using Metrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Support
{
    /// <summary>
    /// Class to centralize metrics based on Metrics.NET
    /// </summary>
    public static class MetricsHelper
    {
        private const String _checkpointToDispatchGaugeName ="checkpoint-to-dispatch";

        private const String _commitPollingClientBufferSizeGaugeName = "polling-client-buffer-size";

        /// <summary>
        /// Tells me how many concurrent commits we are dispatching.
        /// </summary>
        private const String _projectionEngineCurrentDispatchCount = "N° actual concurrent commit dispatch";

        public static void SetProjectionEngineCurrentDispatchCount(Func<Double> valueProvider)
        {
            Metric.Gauge(_projectionEngineCurrentDispatchCount, valueProvider, Unit.Custom("Commits"));
        }

        public static void SetCheckpointCountToDispatch(String slotName, Func<Double> valueProvider)
        {
            String gaugeName;
            if (!string.IsNullOrEmpty(slotName))
            {
                gaugeName = _checkpointToDispatchGaugeName + "-" + slotName;
            }
            else
            {
                gaugeName = _checkpointToDispatchGaugeName;
            }
            if (TenantContext.CurrentTenantId != null) 
            {
                gaugeName = "t[" + TenantContext.CurrentTenantId + "]" + gaugeName;
            }
            Metric.Gauge(gaugeName, valueProvider, Unit.Items);
        }

        public static void SetCommitPollingClientBufferSize(String pollerName, Func<Double> valueProvider)
        {
            String gaugeName;
            if (!string.IsNullOrEmpty(pollerName))
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

        private static readonly Dictionary<String, Meter> CommitDispatchIndex = new Dictionary<string, Meter>();

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

        public static void IncrementProjectionCounter(String projectionName, String slotName, Int64 milliseconds)
        {
            projectionCounter.Increment(projectionName, milliseconds);
            projectionSlotCounter.Increment(slotName, milliseconds);
        }

    }
}

using System;
using System.Linq;
using Castle.Core;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Metrics;

namespace Jarvis.Framework.Kernel.Support
{
    public class ProjectionMetricsConfigurer : IStartable
    {
        private readonly IProjectionStatusLoader _loader;
        private readonly int _maxSkewForSlot;

        /// <summary>
        /// Construct the metric configurer
        /// </summary>
        /// <param name="loader"></param>
        /// <param name="maxSkewForSlot">Maximum value tolerated for a slot 
        /// to be behind latest commit, a value greater failed health check</param>
        public ProjectionMetricsConfigurer(
            IProjectionStatusLoader loader,
            Int32 maxSkewForSlot = 100)
        {
            _loader = loader;
            _maxSkewForSlot = maxSkewForSlot;
        }

        /// <summary>
        /// Number of checkpoint to dispatch during normal operation, it is calculated with maximum
        /// id of the commit in the commit store and the actual dispatched value
        /// </summary>
        private const String CheckpointBehind = "checkpoint-behind";

        /// <summary>
        /// Used during rebuild, tells how much commit should still be dispatched. 
        /// </summary>
        /// <param name="slotName"></param>
        /// <param name="valueProvider"></param>
        public static void SetCheckpointBehind(String slotName, Func<Double> valueProvider)
        {
            String gaugeName;
            if (!string.IsNullOrEmpty(slotName))
            {
                gaugeName = CheckpointBehind + "-" + slotName;
            }
            else
            {
                gaugeName = CheckpointBehind;
            }

            Metric.Gauge(gaugeName, valueProvider, Unit.Items);
        }

        public void Start()
        {
            var stats = _loader.GetSlotMetrics();
            foreach (var stat in stats)
            {
                var slotName = stat.Name;
                SetCheckpointBehind(stat.Name, () => _loader.GetSlotMetric(slotName).CommitBehind);
                HealthChecks.RegisterHealthCheck("Slot-" + slotName, CheckSlotHealth(slotName));
            }
            SetCheckpointBehind("ALLSLOT", () => _loader.GetSlotMetrics().Max(d => d.CommitBehind));
        }

        private Func<HealthCheckResult> CheckSlotHealth(string slotName)
        {
            return () =>
            {
                if (RebuildSettings.ShouldRebuild)
                    return HealthCheckResult.Healthy("Slot " + slotName + " is rebuilding");

                var behind = _loader.GetSlotMetric(slotName).CommitBehind;
                if (behind > _maxSkewForSlot)
                    return HealthCheckResult.Unhealthy("Slot " + slotName + " behind:" + behind);
                else
                    return HealthCheckResult.Healthy("Slot " + slotName + " behind:" + behind);
            };
        }

        public void Stop()
        {
            // Method intentionally left empty.
        }
    }
}

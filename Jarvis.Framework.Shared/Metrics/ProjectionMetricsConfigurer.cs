using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Core;
using Metrics;

namespace Jarvis.Framework.Shared.Metrics
{
    public class ProjectionMetricsConfigurer : IStartable
    {
        private readonly IProjectionStatusLoader _loader;

        public ProjectionMetricsConfigurer(IProjectionStatusLoader loader)
        {
            _loader = loader;
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
            }
            SetCheckpointBehind("ALLSLOT", () => _loader.GetSlotMetrics().Max(d => d.CommitBehind));
        }

        public void Stop()
        {
            
        }
    }
}

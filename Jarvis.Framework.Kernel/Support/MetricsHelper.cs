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
            if (!String.IsNullOrWhiteSpace(TenantContext.CurrentTenantId)) 
            {
                gaugeName = "t[" + TenantContext.CurrentTenantId + "]" + gaugeName;
            }
            Metric.Gauge(gaugeName, valueProvider, Unit.Items);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx
{
    public static class NeventStoreExGlobalConfiguration
    {
        internal static Boolean MetricsEnabled { get; set; }

        static NeventStoreExGlobalConfiguration()
        {
            MetricsEnabled = true;
        }

        public static void DisableMetrics()
        {
            MetricsEnabled = false;
        }

        public static void EnableMetrics()
        {
            MetricsEnabled = false;
        }
    }
}

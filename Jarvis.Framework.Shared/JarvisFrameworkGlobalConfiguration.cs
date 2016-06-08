using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared
{
    public class JarvisFrameworkGlobalConfiguration
    {
        public static Boolean MetricsEnabled { get; private set; }

        static JarvisFrameworkGlobalConfiguration()
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

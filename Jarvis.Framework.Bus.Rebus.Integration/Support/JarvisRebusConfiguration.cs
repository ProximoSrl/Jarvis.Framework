using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
    public class JarvisRebusConfiguration
    {
        public String InputQueue { get; set; }

        public String ErrorQueue { get; set; }

        public Int32 NumOfWorkers { get; set; }

        public Int32 MaxRetry { get; set; }

        public Dictionary<String, String> endpointsMap = new Dictionary<string, string>();

    }
}

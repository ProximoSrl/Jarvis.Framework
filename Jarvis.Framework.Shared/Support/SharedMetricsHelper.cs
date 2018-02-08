using Metrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Support
{
    public static class SharedMetricsHelper
    {
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

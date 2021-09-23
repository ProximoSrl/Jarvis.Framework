using Jarvis.Framework.Kernel.Events;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public class ProjectionMetrics
    {
        public sealed class Meter
        {
            public sealed class Counter
            {
                public string Name { get; private set; }

                public long Calls { get; private set; }

                [BsonIgnore]
                public long Elapsed { get; private set; }

                [BsonElement("Elapsed")]
                public double Seconds
                {
                    get { return ((double)Elapsed / TimeSpan.TicksPerSecond); }
                    set { Elapsed = (long)(value * TimeSpan.TicksPerSecond); }
                }

                public Counter(string name)
                {
                    Name = name;
                }

                public Counter Inc(long ticks)
                {
                    Calls++;
                    Elapsed += ticks;
                    return this;
                }
            }

            private ConcurrentDictionary<string, Counter> Counters { get; set; }

            [BsonElement("Counters")]
            public IList<Counter> CountersByTime
            {
                get { return Counters.Values.OrderByDescending(x => x.Elapsed).ToArray(); }
                set
                {
                    Counters.Clear();
                    foreach (var counter in value)
                    {
                        Counters.TryAdd(counter.Name, counter);
                    }
                }
            }

            public Meter()
            {
                Counters = new ConcurrentDictionary<string, Counter>();
            }

            public long TotalEvents
            {
                get { return Counters.Select(x => x.Value.Calls).Sum(); }
            }

            public long TotalElapsed
            {
                get { return Counters.Select(x => x.Value.Elapsed).Sum(); }
            }

            public void Inc(string counterName, long ticks)
            {
                Counters.AddOrUpdate(counterName,
                    s => (new Counter(s)).Inc(ticks),
                    (s, counter) => counter.Inc(ticks)
                    );
            }
        }

        private readonly IDictionary<string, Meter> _meters;

        public ProjectionMetrics(IEnumerable<IProjection> projections)
        {
            _meters = new Dictionary<string, Meter>((int)(projections.Count() * 1.3));
            foreach (var projection in projections)
            {
                _meters.Add(projection.Info.CommonName, new Meter());
            }
        }

        public void Inc(string name, string counter, long ticks)
        {
            _meters[name].Inc(counter, ticks);
        }

        public Meter GetMeter(string name)
        {
            return _meters[name];
        }
    }
}
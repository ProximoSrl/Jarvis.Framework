using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    public class RebuildStatus
    {
        private int _bucketActiveCount = 0;

        public int BucketActiveCount => _bucketActiveCount; 

        public List<BucketSummaryStatus> SummaryStatus { get; set; }

        public Boolean Done => BucketActiveCount == 0;

        private readonly ConcurrentBag<string> _activeBuckets = new ConcurrentBag<string>();

        public IReadOnlyCollection<string> ActiveBuckets => _activeBuckets;

        public RebuildStatus()
        {
            SummaryStatus = new List<BucketSummaryStatus>();
        }

        public void AddBucket(string bucketSlots)
        {
            Interlocked.Increment(ref _bucketActiveCount);
            _activeBuckets.Add(bucketSlots);
        }

        public void BucketDone(
            String bucketDescription,
            Int64 eventsDispatched,
            Int64 durationInMilliseconds,
            Int64 totalEventsToDispatchWithoutFiltering)
        {
            Interlocked.Decrement(ref _bucketActiveCount);
            SummaryStatus.Add(new BucketSummaryStatus()
            {
                BucketDescription = bucketDescription,
                EventsDispatched = eventsDispatched,
                DurationInMilliseconds = durationInMilliseconds,
                TotalEventsToDispatchWithoutFiltering = totalEventsToDispatchWithoutFiltering,
            });
        }

        public class BucketSummaryStatus
        {
            public String BucketDescription { get; set; }

            /// <summary>
            /// For RebuildProjectionEngine (V1) these are the number of events in NStore, for V2 it is
            /// the number of CHUNKS (not single events)
            /// </summary>
            public Int64 TotalEventsToDispatchWithoutFiltering { get; set; }

            /// <summary>
            /// For RebuildProjectionEngine (V1) these are the number of events dispatched, for V2 it is
            /// the number of CHUNKS (not single events)
            /// </summary>
            public Int64 EventsDispatched { get; set; }

            /// <summary>
            /// Rebuild duration.
            /// </summary>
            public Int64 DurationInMilliseconds { get; set; }
        }
    }
}

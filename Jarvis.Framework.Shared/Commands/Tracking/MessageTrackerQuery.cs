using System;
using System.Linq;
using MongoDB.Driver.Linq;

namespace Jarvis.Framework.Shared.Commands.Tracking
{
    public sealed class MessageTrackerQuery
    {
        public Boolean? Completed { get; private set; }

        public Boolean? Failed { get; private set; }

        public String User { get; private set; }

        public DateTime? FromDate { get; private set; }

        public String AggregateId { get; private set; }

        public MessageTrackerQuery GetCompleted()
        {
            Completed = true;
            return this;
        }

        public MessageTrackerQuery GetPending()
        {
            Completed = false;
            return this;
        }

        public MessageTrackerQuery GetFailed()
        {
            Failed = true;
            return this;
        }

        public MessageTrackerQuery GetSucceeded()
        {
            Failed = false;
            return this;
        }

        public MessageTrackerQuery ForUser(String userName)
        {
            User = userName;
            return this;
        }

        public MessageTrackerQuery MoreRecentThan(DateTime fromDate)
        {
            FromDate = fromDate;
            return this;
        }

        public MessageTrackerQuery ForAggregate(String aggregateId)
        {
            AggregateId = aggregateId;
            return this;
        }

        public IQueryable<TrackedMessageModel> ComposeLinqQuery(
            IQueryable<TrackedMessageModel> linqQuery)
        {
            if (!String.IsNullOrEmpty(AggregateId))
                linqQuery = linqQuery.Where(m => m.AggregateId == AggregateId);
            if (Completed.HasValue)
                linqQuery = linqQuery.Where(m => m.Completed == Completed.Value);
            if (Failed.HasValue)
                linqQuery = linqQuery.Where(m => m.Completed == true && m.Success == !Failed.Value);
            if (!String.IsNullOrEmpty(User))
                linqQuery = linqQuery.Where(m => m.IssuedBy == User);
            if (FromDate.HasValue)
                linqQuery = linqQuery.Where(m => m.StartedAt > FromDate.Value);

            return linqQuery.OrderByDescending(m => m.StartedAt);
        }
    }
}

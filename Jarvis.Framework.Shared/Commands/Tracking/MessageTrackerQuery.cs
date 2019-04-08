using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
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

        public FilterDefinition<TrackedMessageModel> CreateFilter()
        {
            List<FilterDefinition<TrackedMessageModel>> filters = new List<FilterDefinition<TrackedMessageModel>>();

            if (!String.IsNullOrEmpty(AggregateId))
            {
                filters.Add(Builders<TrackedMessageModel>.Filter.Eq(m => m.AggregateId, AggregateId));
            }
            if (Completed.HasValue)
            {
                filters.Add(Builders<TrackedMessageModel>.Filter.Eq(m => m.Completed, Completed.Value));
            }
            if (Failed.HasValue)
            {
                filters.Add(Builders<TrackedMessageModel>.Filter.Eq(m => m.Success, !Failed.Value));
            }
            if (!String.IsNullOrEmpty(User))
            {
                filters.Add(Builders<TrackedMessageModel>.Filter.Eq(m => m.IssuedBy, User));
            }
            if (FromDate.HasValue)
            {
                filters.Add(Builders<TrackedMessageModel>.Filter.Gt(m => m.StartedAt, FromDate.Value));
            }

            return Builders<TrackedMessageModel>.Filter.And(filters);
        }
    }
}

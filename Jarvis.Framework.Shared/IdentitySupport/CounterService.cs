using System;
using Jarvis.Framework.Shared.MultitenantSupport;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public class CounterService : ICounterService
    {
        readonly MongoCollection<IdentityCounter> _counters;

        public class IdentityCounter
        {
            public string Id { get; set; }
            public long Last { get; set; }
        }

        public CounterService(MongoDatabase db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _counters = db.GetCollection<IdentityCounter>("sysCounters");
        }

        public long GetNext(string serie)
        {
            var result = _counters.FindAndModify(new FindAndModifyArgs()
            {
                Query = Query<IdentityCounter>.EQ(x => x.Id, serie),
                Update = Update<IdentityCounter>.Inc(x => x.Last, 1),
                SortBy = SortBy.Null,
                VersionReturned = FindAndModifyDocumentVersion.Modified,
                Upsert = true
            });

            var counter = result.GetModifiedDocumentAs<IdentityCounter>().Last;
            return counter;
        }
    }

    public class InMemoryCounterService : ICounterService
    {
        long _last = 0;

        public long GetNext(string serie)
        {
            return ++_last;
        }
    }

    public class MultitenantCounterService : ICounterService
    {
        readonly ITenantAccessor _tenantAccessor;

        public MultitenantCounterService(ITenantAccessor tenantAccessor)
        {
            _tenantAccessor = tenantAccessor;
        }

        public long GetNext(string serie)
        {
            return _tenantAccessor.Current.CounterService.GetNext(serie);
        }
    }
}
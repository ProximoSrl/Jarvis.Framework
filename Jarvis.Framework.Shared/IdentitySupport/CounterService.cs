﻿using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.MultitenantSupport;
using MongoDB.Driver;
using System;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public class CounterService : IReservableCounterService, ICounterServiceWithOffset
    {
        protected readonly IMongoCollection<IdentityCounter> _counters;

        public class IdentityCounter
        {
            public string Id { get; set; }
            public long Last { get; set; }
        }

        public CounterService(IMongoDatabase db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _counters = db.GetCollection<IdentityCounter>("sysCounters");
        }

        public long GetNext(string serie)
        {
            Int32 count = 0;
            while (count++ < 5)
            {
                try
                {
                    var counter = _counters.FindOneAndUpdate(
                                    Builders<IdentityCounter>.Filter.Eq(x => x.Id, serie),
                                    Builders<IdentityCounter>.Update.Inc(x => x.Last, 1),
                                    new FindOneAndUpdateOptions<IdentityCounter, IdentityCounter>()
                                    {
                                        ReturnDocument = ReturnDocument.After,
                                        IsUpsert = true,
                                    });
                    return counter.Last;
                }
                catch (MongoCommandException ex)
                {
                    //We can tolerate only duplicate exception
                    if (!ex.Message.Contains("E11000"))
                        throw;
                }
            }

            throw new JarvisFrameworkEngineException("Unable to generate next number for serie " + serie);
        }

        public ReservationSlot Reserve(string serie, int amount)
        {
            var counter = _counters.FindOneAndUpdate(
                                   Builders<IdentityCounter>.Filter.Eq(x => x.Id, serie),
                                   Builders<IdentityCounter>.Update.Inc(x => x.Last, amount),
                                   new FindOneAndUpdateOptions<IdentityCounter, IdentityCounter>()
                                   {
                                       ReturnDocument = ReturnDocument.After,
                                       IsUpsert = true,
                                   });
            return new ReservationSlot(counter.Last - amount + 1, counter.Last);
        }

        public void EnsureMinimumValue(String serie, Int64 minValue)
        {
            if (minValue <= 0) return; //Nothing to be done.

            var actualIndex = _counters.FindOneById(serie);
            if (actualIndex == null)
            {
                _counters.InsertOne(new IdentityCounter()
                {
                    Id = serie,
                    Last = minValue - 1
                });
            }
            else
            {
                if (actualIndex.Last < minValue)
                {
                    actualIndex.Last = minValue - 1;
                    _counters.Save(actualIndex, actualIndex.Id);
                }
            }
        }
    }

    public class InMemoryCounterService : IReservableCounterService
    {
        long _last = 0;

        public long GetNext(string serie)
        {
            return ++_last;
        }

        public ReservationSlot Reserve(string serie, int amount)
        {
            var low = _last + 1;
            var high = low + amount;
            _last = high + 1;
            return new ReservationSlot(low, high);
        }
    }

    public class MultitenantCounterService : ICounterService
    {
        private readonly ITenantAccessor _tenantAccessor;

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
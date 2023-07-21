using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

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

        public async Task<long> GetNextAsync(string serie)
        {
            Int32 count = 0;
            while (count++ < 5)
            {
                try
                {
                    var counter = await _counters.FindOneAndUpdateAsync(
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

        public async Task ForceNextIdAsync(string serie, long nextIdToReturn)
        {
            if (nextIdToReturn <= 0)
            {
                throw new JarvisFrameworkEngineException($"Unable to force next id for serie {serie} to {nextIdToReturn} because it is not a valid value.");
            }
            
            //we need to understand if we already have a record, to avoid concurrency issues
            var actualIndex = _counters.FindOneById(serie);
            if (actualIndex == null)
            {
                //this can fail if for high concurrency we have another thread that insert record
                //here, it is a really strange situation, but it can happen.
                _counters.InsertOne(new IdentityCounter()
                {
                    Id = serie,
                    Last = nextIdToReturn - 1
                });
            }
            else
            {
                //we need to update the counter only if the new value is greater than the current one.
                var last = nextIdToReturn - 1;
                var result = await _counters.FindOneAndUpdateAsync(
                       Builders<IdentityCounter>.Filter.And(
                            Builders<IdentityCounter>.Filter.Eq(x => x.Id, serie),
                            Builders<IdentityCounter>.Filter.Lt(x => x.Last, last)
                        ),
                        Builders<IdentityCounter>.Update.Set(x => x.Last, last),
                        new FindOneAndUpdateOptions<IdentityCounter, IdentityCounter>()
                        {
                            ReturnDocument = ReturnDocument.After,
                            IsUpsert = false,
                        });

                if (result == null)
                {
                    throw new JarvisFrameworkEngineException($"Unable to force next id for serie {serie} to {nextIdToReturn} because it is already greater than current value.");
                }
            }
        }

        public async Task<long> PeekNextAsync(string serie)
        {
            var recordCursor = await _counters.FindAsync(
                Builders<IdentityCounter>.Filter.Eq(x => x.Id, serie));
            var record = await recordCursor.FirstOrDefaultAsync();
            return record == null ? 1 : record.Last + 1;
        }
    }
}
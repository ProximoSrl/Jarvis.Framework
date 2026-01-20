using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Driver;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    /// <summary>
    /// This is the counter service class based on mongodb. It is capable of generating
    /// counters in a thread safe way.
    /// </summary>
    public class CounterService : IReservableCounterService, ICounterServiceWithOffset
    {
        protected readonly IMongoCollection<IdentityCounter> _counters;
        private readonly ConcurrentDictionary<string, long> _lastKnownValues = new ConcurrentDictionary<string, long>();

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

        /// <summary>
        /// Updates the cache only if the new value is greater than the existing cached value.
        /// This ensures thread-safety when multiple operations complete out of order.
        /// </summary>
        private void UpdateCacheIfGreater(string serie, long newValue)
        {
            _lastKnownValues.AddOrUpdate(
                serie,
                newValue,
                (key, existingValue) => Math.Max(existingValue, newValue));
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

            // Use $max operator to atomically ensure the value is at least minValue - 1.
            // This fixes a race condition in the previous implementation which read, checked, and then saved the document.
            _counters.FindOneAndUpdate(
                Builders<IdentityCounter>.Filter.Eq(x => x.Id, serie),
                Builders<IdentityCounter>.Update.Max(x => x.Last, minValue - 1),
                new FindOneAndUpdateOptions<IdentityCounter, IdentityCounter>()
                {
                    IsUpsert = true,
                    ReturnDocument = ReturnDocument.After
                });
        }

        public async Task<long> GetNextAsync(string serie, CancellationToken cancellationToken = default)
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
                        }, cancellationToken);
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

        public async Task ForceNextIdAsync(string serie, long nextIdToReturn, CancellationToken cancellationToken = default)
        {
            if (nextIdToReturn <= 0)
            {
                throw new JarvisFrameworkEngineException($"Unable to force next id for serie {serie} to {nextIdToReturn} because it is not a valid value.");
            }

            long targetValue = nextIdToReturn - 1;

            // Use $max to ensure atomicity and correct upsert behavior without race conditions
            var result = await _counters.FindOneAndUpdateAsync(
                     Builders<IdentityCounter>.Filter.Eq(x => x.Id, serie),
                     Builders<IdentityCounter>.Update.Max(x => x.Last, targetValue),
                     new FindOneAndUpdateOptions<IdentityCounter, IdentityCounter>()
                     {
                         ReturnDocument = ReturnDocument.After,
                         IsUpsert = true,
                     }, cancellationToken);

            // If the database value is greater than our target, it means we couldn't force it backwards.
            if (result.Last > targetValue)
            {
                throw new JarvisFrameworkEngineException($"Unable to force next id for serie {serie} to {nextIdToReturn} because it is already greater than current value.");
            }
        }

        public async Task<long> PeekNextAsync(string serie, CancellationToken cancellationToken = default)
        {
            // Always query the database to guarantee correctness in distributed environments.
            // The cache is only used for CheckValidityAsync optimization.

            var recordCursor = await _counters.FindAsync(
                Builders<IdentityCounter>.Filter.Eq(x => x.Id, serie),
                cancellationToken: cancellationToken);
            var record = await recordCursor.FirstOrDefaultAsync(cancellationToken);

            if (record != null)
            {
                return record.Last + 1;
            }

            return 1;
        }

        public async Task<bool> CheckValidityAsync(string serie, long value, CancellationToken cancellationToken = default)
        {
            // Quick path: if we have a cached value and the value to check is <= cached value,
            // we know for certain it's valid (already generated) without hitting the database.
            // The cache can only be stale in one direction: the real DB value can be higher,
            // never lower. So if value <= cachedLast, it's definitely valid.
            if (_lastKnownValues.TryGetValue(serie, out var cachedLast) && value <= cachedLast)
            {
                return true;
            }

            // Cache miss or value > cachedLast: we need to query the database
            // to get the authoritative current value
            var recordCursor = await _counters.FindAsync(
                Builders<IdentityCounter>.Filter.Eq(x => x.Id, serie),
                cancellationToken: cancellationToken);
            var record = await recordCursor.FirstOrDefaultAsync(cancellationToken);

            if (record != null)
            {
                // Update cache with the fresh value from database
                UpdateCacheIfGreater(serie, record.Last);
                return value <= record.Last;
            }

            // No record exists, so no values have been generated yet
            return false;
        }
    }
}
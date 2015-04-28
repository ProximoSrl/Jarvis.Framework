using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class CachedMongoStorage2<TModel, TKey> : IMongoStorage<TModel, TKey> where TModel : class, IReadModelEx<TKey>
    {
        readonly MongoStorage<TModel, TKey> _storage;

        public CachedMongoStorage2(MongoCollection<TModel> collection)
        {
            _storage = new MongoStorage<TModel, TKey>(collection);
        }

        public bool IndexExists(IMongoIndexKeys keys)
        {
            return _storage.IndexExists(keys);
        }

        #region Cache

        private readonly ConcurrentDictionary<TKey, TModel> _cache = new ConcurrentDictionary<TKey, TModel>();
        private readonly ConcurrentDictionary<TKey, Boolean> _cacheDeleted = new ConcurrentDictionary<TKey, Boolean>();

        public Boolean IsCacheActive { get; private set; }
        //when collection is empty we can batch insert.
        private Boolean _isCollectionEmpty;

        private void AddInCache(TKey id, TModel value)
        {
            _cache[id] = value;
            Boolean outRef;
            _cacheDeleted.TryRemove(id, out outRef);
            if (_autoFlushCount > 0 && _cache.Count >= _autoFlushCount)
            {
                Flush();
                _cache.Clear();
            }
        }

        //private class CacheEntry<TModel>
        //{
        //    public TModel Entry { get; set; }

        //    public DateTime LastAccess { get; set; }

        //    //public CacheStatus Status { get; set; }

        //    public CacheEntry<TModel>(TModel entry)
        //    {
        //        Entry = entry;
        //        LastAccess = DateTime.Now;
        //    }
        //}

        //private enum CacheStatus
        //{ 
        //    Inserted = 0,
        //    Deleted = 1
        //}

        #endregion

        #region AutoFlush

        private Timer _timer;

        private Int32 _autoFlushCount = -1;

        /// <summary>
        /// Set timeout in milliseconds for the autoflushing timer.
        /// </summary>
        /// <param name="timeoutInMilliseconds">Timeout in Milliseconds, 0 or negative
        /// values if you want to disable autoflush.</param>
        public void SetAutoFlush(Int32 timeoutInMilliseconds)
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
            if (timeoutInMilliseconds >= 0)
            {
                _timer = new Timer(TimerCallbackFunction, null, timeoutInMilliseconds, timeoutInMilliseconds);
            }
        }

        private void TimerCallbackFunction(object state)
        {
            Flush();
        }

        #endregion

        void FlushBeforeMongoAccess()
        {
            if (IsCacheActive)
                Flush();
        }

        public void CreateIndex(IMongoIndexKeys keys, IMongoIndexOptions options = null)
        {
            _storage.CreateIndex(keys, options);
        }

        public void InsertBatch(IEnumerable<TModel> values)
        {
            if (IsCacheActive)
            {
                foreach (var value in values)
                {
                    AddInCache(value.Id, value);
                }
            }
            else
            {
                _storage.InsertBatch(values);
            }
        }

        public IQueryable<TModel> All
        {
            get
            {
                return IsCacheActive ? _cache.Values.AsQueryable() : _storage.All;
            }
        }

        public TModel FindOneById(TKey id)
        {
            if (IsCacheActive)
            {
                if (_cache.ContainsKey(id))
                    return _cache[id];

                var storageData = _storage.FindOneById(id);
                AddInCache(id, storageData);
                return storageData;
            }
            else
            {
                return _storage.FindOneById(id);
            }
        }

        public IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter)
        {
            return IsCacheActive ?
                _cache.Values.AsQueryable().Where(filter).ToArray().AsQueryable() :
                _storage.Where(filter);
        }

        public bool Contains(Expression<Func<TModel, bool>> filter)
        {
            return IsCacheActive ?
                _cache.Values.AsQueryable().Any(filter) :
                _storage.Contains(filter);
        }

        public InsertResult Insert(TModel model)
        {
            if (IsCacheActive)
            {
                try
                {
                    AddInCache(model.Id, model);
                    return new InsertResult()
                    {
                        Ok = true
                    };
                }
                catch (Exception ex)
                {
                    return new InsertResult()
                    {
                        Ok = false,
                        ErrorMessage = ex.Message
                    };
                }
            }
            return _storage.Insert(model);
        }

        public SaveResult SaveWithVersion(TModel model, int orignalVersion)
        {
            if (IsCacheActive)
            {
                // non posso controllare le versioni perché l'istanza è la stessa
                AddInCache(model.Id, model);
                return new SaveResult { Ok = true };
            }

            return _storage.SaveWithVersion(model, orignalVersion);
        }

        public void Save(TModel model)
        {
            if (IsCacheActive)
            {
                // non posso controllare le versioni perché l'istanza è la stessa
                AddInCache(model.Id, model);
            }
            else
            {
                _storage.Save(model);
            }

        }

        public DeleteResult Delete(TKey id)
        {
            if (IsCacheActive)
            {
                TModel outRef;
                var removed = _cache.TryRemove(id, out outRef);
                _cacheDeleted[id] = true;
                return new DeleteResult
                {
                    Ok = true,
                    DocumentsAffected = removed ? 1 : 0
                };
            }

            return _storage.Delete(id);
        }

        public void Drop()
        {
            _storage.Drop();
        }

        public MongoCollection<TModel> Collection
        {
            get
            {
                FlushBeforeMongoAccess();
                return _storage.Collection;
            }
        }
        public IEnumerable<BsonDocument> Aggregate(IEnumerable<BsonDocument> operations)
        {
            FlushBeforeMongoAccess();
            return _storage.Aggregate(operations);
        }

        public IEnumerable<BsonDocument> Aggregate(AggregateArgs aggregateArgs)
        {
            FlushBeforeMongoAccess();
            return _storage.Aggregate(aggregateArgs);
        }

        Int32 flushExecuting = 0;

        public void Flush()
        {
            if (IsCacheActive)
            {
                if (Interlocked.CompareExchange(ref flushExecuting, 1, 0) == 0)
                {
                    try
                    {
                        if (_cache.Any())
                        {
                            var toInsert = _cache.Values.ToList();
                            if (_isCollectionEmpty)
                            {
                                _storage.InsertBatch(toInsert);
                            }
                            else
                            {
                                Parallel.ForEach(toInsert, e => _storage.Save(e));
                            }
                        }
                        if (_cacheDeleted.Any() && !_isCollectionEmpty)
                        {
                            var toDeleted = _cacheDeleted.Keys.ToList();
                            Parallel.ForEach(
                                  _cacheDeleted,
                                  entry => _storage.Delete(entry.Key));
                            foreach (var deleted in toDeleted)
                            {
                                Boolean outRef;
                                _cacheDeleted.TryRemove(deleted, out outRef);
                            }
                        }
                        CheckCollectionEmpty();
                    }
                    finally
                    {
                        Interlocked.Exchange(ref flushExecuting, 0);
                    }
                }

            }
        }

        public void DisableCache()
        {
            Flush();
            _cache.Clear();
            _cacheDeleted.Clear();
            IsCacheActive = false;
        }

        public void EnableCache()
        {
            IsCacheActive = true;
            CheckCollectionEmpty();
        }

        private void CheckCollectionEmpty()
        {
            _isCollectionEmpty = _storage.All.Any() == false;
        }


        /// <summary>
        /// When this variable is greater than 0 we want no more than approx
        /// autoFlushCount objects in memory cache, when that threshold is reached
        /// we want to flush and reduce number of objects in memory.
        /// </summary>
        /// <param name="autoFlushCount"></param>
        public void SetAutoFlushOnCount(int autoFlushCount)
        {
            _autoFlushCount = autoFlushCount;
        }
    }


}
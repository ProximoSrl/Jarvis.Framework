using System;
using System.Collections.Concurrent;
using System.Runtime.Caching;
using System.Threading;
using Jarvis.NEventStoreEx.Support;
using Metrics;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
    public class AggregateCachedRepositoryFactory : IAggregateCachedRepositoryFactory
    {
        private static readonly MemoryCache Cache = new MemoryCache("RepositorySingleAggregateFactory");

        private static readonly Counter CacheHitCounter = Metric.Counter("RepositorySingleEntityCacheHits", Unit.Calls);
        private static readonly Counter CacheMissCounter = Metric.Counter("RepositorySingleEntityCacheMisses", Unit.Calls);

        static readonly CacheItemPolicy CachePolicy = new CacheItemPolicy()
        {
            SlidingExpiration = TimeSpan.FromMinutes(10),
            RemovedCallback = RemovedCallback
        };

        private class CacheEntry
        {
            public IRepositoryEx RepositoryEx { get; private set; }

            public IAggregateEx Aggregate { get; private set; }

            public Boolean InUse { get; set; }

            public CacheEntry(IRepositoryEx repositoryEx, IAggregateEx aggregate)
            {
                RepositoryEx = repositoryEx;
                Aggregate = aggregate;
            }
        }

        private static void RemovedCallback(CacheEntryRemovedArguments arguments)
        {
            var cacheEntry = arguments.CacheItem.Value as CacheEntry;
            if (cacheEntry != null && cacheEntry.RepositoryEx != null)
                cacheEntry.RepositoryEx.Dispose();
        }

        readonly Func<IRepositoryEx> _repositoryFactory;
        private readonly bool _cacheDisabled;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repositoryFactory"></param>
        /// <param name="cacheDisabled">If True cache is not used, and each time an instance of
        /// <see cref="IAggregateCachedRepository{TAggregate}"/> is requested, a non caceable
        /// entities is returned.
        /// <br />
        /// If this value is true this class also do not try to have a lock on aggregate id thus
        /// it does not prevent two thread to execute at the same moment on the same aggregateId.
        /// </param>
        public AggregateCachedRepositoryFactory(
            Func<IRepositoryEx> repositoryFactory,
            Boolean cacheDisabled = false)
        {
            _repositoryFactory = repositoryFactory;
            _cacheDisabled = cacheDisabled;
        }

        Int32 _isGettingCache = 0;

        IAggregateCachedRepository<TAggregate> IAggregateCachedRepositoryFactory.Create<TAggregate>(IIdentity id)
        {
            if (!_cacheDisabled)
            {
                //lock is necessary because only if a lock is aquired we can be 100% sure that a single thread is accessing
                //cached repository and entity.
                CacheEntry cached = GetCache(id);

                IRepositoryEx innerRepository;
                TAggregate aggregate = null;
                if (cached != null)
                {
                    CacheHitCounter.Increment();
                    innerRepository = cached.RepositoryEx;
                    aggregate = cached.Aggregate as TAggregate;
                }
                else
                {
                    //I have nothing in cache, I'll create a new repository and since aggregate is 
                    //null it will load the entity for the first time.
                    CacheMissCounter.Increment();
                    innerRepository = _repositoryFactory();
                }

                var repoSingleEntity = new CachableAggregateCachedRepository<TAggregate>(
                    innerRepository,
                    aggregate,
                    DisposeRepository,
                    AfterSave,
                    OnException,
                    id);

                return repoSingleEntity;

            }
            return new SingleUseAggregateCachedRepository<TAggregate>(_repositoryFactory(), id);
        }

        private CacheEntry GetCache(IIdentity id)
        {
            CacheEntry cached = null;
            try
            {
                if (Interlocked.CompareExchange(ref _isGettingCache, 1, 0) == 0)
                {
                    cached = Cache.Get(id.AsString()) as CacheEntry;
                    Cache.Remove(id.AsString());
                    if (cached == null || cached.InUse) return null;
                    cached.InUse = true;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isGettingCache, 0);
            }

            return cached;
        }

        private void DisposeRepository(IIdentity id, IRepositoryEx repositoryEx)
        {
            //This is called when the repository is disposed.
            ReleaseAggregateId(id);
        }

        private void AfterSave(IIdentity id, IRepositoryEx repositoryEx, IAggregateEx aggregate)
        {
            //store the repository on the cache, actually we are keeping the very
            //same entity that was disposed by the caller.
            var cacheEntry = new CacheEntry(repositoryEx, aggregate);
            Cache.Add(id.AsString(), cacheEntry, CachePolicy);
        }

        private void OnException(IIdentity id, IRepositoryEx repositoryEx)
        {
            //remove the repository from cache, this will really dispose wrapped repository
            Cache.Remove(id.AsString());
        }


        private void ReleaseAggregateId(IIdentity id)
        {

        }
    }
}
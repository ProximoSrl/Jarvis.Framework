#if NETSTANDARD
using Jarvis.Framework.Shared.IdentitySupport;
using Metrics;
using Microsoft.Extensions.Caching.Memory;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Jarvis.Framework.Shared.Persistence.EventStore
{
    public class AggregateCachedRepositoryFactory : IAggregateCachedRepositoryFactory
    {
        private static readonly MemoryCache Cache = new MemoryCache(new MemoryCacheOptions());

        private static readonly Counter CacheHitCounter = Metric.Counter("RepositorySingleEntityCacheHits", Unit.Calls);
        private static readonly Counter CacheMissCounter = Metric.Counter("RepositorySingleEntityCacheMisses", Unit.Calls);
        private readonly MemoryCacheEntryOptions _memoryCacheEntryOptions;

        private class CacheEntry
        {
            public IRepository RepositoryEx { get; }

            public Boolean InUse { get; set; }

            public CacheEntry(IRepository repositoryEx)
            {
                RepositoryEx = repositoryEx;
            }
        }

        public AggregateCachedRepositoryFactory()
        {
            _memoryCacheEntryOptions = new MemoryCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            };
            _memoryCacheEntryOptions.RegisterPostEvictionCallback(PostEvictionCallback);
        }

        private void PostEvictionCallback(object key, object value, EvictionReason reason, object state)
        {
            var cacheEntry = value as CacheEntry;
            if (cacheEntry != null && cacheEntry.RepositoryEx != null && !cacheEntry.InUse)
            {
                _repositoryFactory.Release(cacheEntry.RepositoryEx);
            }
        }

        private readonly IRepositoryFactory _repositoryFactory;
        private readonly bool _cacheDisabled;

        /// <summary>
        /// Create aggregate cached repository factory
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
            IRepositoryFactory repositoryFactory,
            Boolean cacheDisabled = false)
        {
            _repositoryFactory = repositoryFactory;
            _cacheDisabled = cacheDisabled;
        }

        private Int32 _isGettingCache = 0;

        public IList<PostEvictionCallbackRegistration> EvictCallback { get; private set; }

        IAggregateCachedRepository<TAggregate> IAggregateCachedRepositoryFactory.Create<TAggregate>(IIdentity id)
        {
            if (!_cacheDisabled)
            {
                //lock is necessary because only if a lock is aquired we can be 100% sure that a single thread is accessing
                //cached repository and entity.
                CacheEntry cached = GetCache(id);

                IRepository innerRepository;
                TAggregate aggregate = null;
                if (cached != null)
                {
                    CacheHitCounter.Increment();
                    innerRepository = cached.RepositoryEx;
                }
                else
                {
                    //I have nothing in cache, I'll create a new repository and since aggregate is 
                    //null it will load the entity for the first time.
                    CacheMissCounter.Increment();
                    innerRepository = _repositoryFactory.Create();
                }

                return new CachableAggregateCachedRepository<TAggregate>(
                    innerRepository,
                    DisposeRepository,
                    AfterSave,
                    OnException,
                    id);
            }
            return new SingleUseAggregateCachedRepository<TAggregate>(_repositoryFactory, id);
        }

        private CacheEntry GetCache(IIdentity id)
        {
            CacheEntry cached = null;
            try
            {
                if (Interlocked.CompareExchange(ref _isGettingCache, 1, 0) == 0)
                {
                    cached = Cache.Get(id.AsString()) as CacheEntry;
                    if (cached == null || cached.InUse) return null;

                    cached.InUse = true;
                    //remover after is in use
                    Cache.Remove(id.AsString()); //does not call dispose because in use.
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isGettingCache, 0);
            }

            return cached;
        }

        private void DisposeRepository(String id, IRepository repository)
        {
            //This is called when the repository is disposed.
            ReleaseAggregateId(id);
        }

        private void AfterSave(String id, IRepository repositoryEx)
        {
            //store the repository on the cache, actually we are keeping the very
            //same entity that was disposed by the caller.
            var cacheEntry = new CacheEntry(repositoryEx);
            Cache.Set(id, cacheEntry, _memoryCacheEntryOptions);
        }

        private void OnException(String id, IRepository repositoryEx)
        {
            //remove the repository from cache, this will really dispose wrapped repository
            _repositoryFactory.Release(repositoryEx);
            Cache.Remove(id);
        }

        private void ReleaseAggregateId(String id)
        {
            // Method intentionally left empty.
        }
    }
}

#else

using Jarvis.Framework.Shared.IdentitySupport;
using Metrics;
using NStore.Domain;
using System;
using System.Runtime.Caching;
using System.Threading;

namespace Jarvis.Framework.Shared.Persistence.EventStore
{
    public class AggregateCachedRepositoryFactory : IAggregateCachedRepositoryFactory
    {
        private static readonly MemoryCache Cache = new MemoryCache("RepositorySingleAggregateFactory");

        private static readonly Counter CacheHitCounter = Metric.Counter("RepositorySingleEntityCacheHits", Unit.Calls);
        private static readonly Counter CacheMissCounter = Metric.Counter("RepositorySingleEntityCacheMisses", Unit.Calls);

        readonly CacheItemPolicy _cachePolicy;

        private class CacheEntry
        {
            public IRepository RepositoryEx { get; }

            public Boolean InUse { get; set; }

            public CacheEntry(IRepository repositoryEx)
            {
                RepositoryEx = repositoryEx;
            }
        }

        private void RemovedCallback(CacheEntryRemovedArguments arguments)
        {
            var cacheEntry = arguments.CacheItem.Value as CacheEntry;
            if (cacheEntry != null && cacheEntry.RepositoryEx != null && !cacheEntry.InUse)
            {
                _repositoryFactory.Release(cacheEntry.RepositoryEx);
            }
        }

        private readonly IRepositoryFactory _repositoryFactory;
        private readonly bool _cacheDisabled;

        /// <summary>
        /// Create aggregate cached repository factory
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
            IRepositoryFactory repositoryFactory,
            Boolean cacheDisabled = false)
        {
            _repositoryFactory = repositoryFactory;
            _cacheDisabled = cacheDisabled;
            _cachePolicy = new CacheItemPolicy()
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                RemovedCallback = RemovedCallback
            };
        }

        private Int32 _isGettingCache = 0;

        IAggregateCachedRepository<TAggregate> IAggregateCachedRepositoryFactory.Create<TAggregate>(IIdentity id)
        {
            if (!_cacheDisabled)
            {
                //lock is necessary because only if a lock is aquired we can be 100% sure that a single thread is accessing
                //cached repository and entity.
                CacheEntry cached = GetCache(id);

                IRepository innerRepository;
                TAggregate aggregate = null;
                if (cached != null)
                {
                    CacheHitCounter.Increment();
                    innerRepository = cached.RepositoryEx;
                }
                else
                {
                    //I have nothing in cache, I'll create a new repository and since aggregate is 
                    //null it will load the entity for the first time.
                    CacheMissCounter.Increment();
                    innerRepository = _repositoryFactory.Create();
                }

                return new CachableAggregateCachedRepository<TAggregate>(
                    innerRepository,
                    DisposeRepository,
                    AfterSave,
                    OnException,
                    id);
            }
            return new SingleUseAggregateCachedRepository<TAggregate>(_repositoryFactory, id);
        }

        private CacheEntry GetCache(IIdentity id)
        {
            CacheEntry cached = null;
            try
            {
                if (Interlocked.CompareExchange(ref _isGettingCache, 1, 0) == 0)
                {
                    cached = Cache.Get(id.AsString()) as CacheEntry;
                    if (cached == null || cached.InUse) return null;

                    cached.InUse = true;
                    //remover after is in use
                    Cache.Remove(id.AsString()); //does not call dispose because in use.
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isGettingCache, 0);
            }

            return cached;
        }

        private void DisposeRepository(String id, IRepository repository)
        {
            //This is called when the repository is disposed.
            ReleaseAggregateId(id);
        }

        private void AfterSave(String id, IRepository repositoryEx)
        {
            //store the repository on the cache, actually we are keeping the very
            //same entity that was disposed by the caller.
            var cacheEntry = new CacheEntry(repositoryEx);
            Cache.Add(id, cacheEntry, _cachePolicy);
        }

        private void OnException(String id, IRepository repositoryEx)
        {
            //remove the repository from cache, this will really dispose wrapped repository
            _repositoryFactory.Release(repositoryEx);
            Cache.Remove(id);
        }

        private void ReleaseAggregateId(String id)
        {
            // Method intentionally left empty.
        }
    }
}
#endif

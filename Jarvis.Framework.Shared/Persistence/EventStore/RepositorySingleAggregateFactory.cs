using App.Metrics;
using App.Metrics.Counter;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Support;
using NStore.Core.Streams;
using NStore.Domain;
using System;
#if NETSTANDARD2_0_OR_GREATER
using Microsoft.Extensions.Caching.Memory;
#else
using System.Runtime.Caching;
#endif

namespace Jarvis.Framework.Shared.Persistence.EventStore
{
	public class AggregateCachedRepositoryFactory : IAggregateCachedRepositoryFactory
	{
#if NETSTANDARD2_0_OR_GREATER
		private static readonly MemoryCache Cache = new MemoryCache(new MemoryCacheOptions());
#else
		private static readonly MemoryCache Cache = new MemoryCache("RepositorySingleAggregateFactory");
		readonly CacheItemPolicy _cachePolicy;
#endif
		private static readonly CounterOptions CacheHitCounter = JarvisFrameworkMetricsHelper.CreateCounter("RepositorySingleEntityCacheHits", Unit.Calls);
		private static readonly CounterOptions CacheMissCounter = JarvisFrameworkMetricsHelper.CreateCounter("RepositorySingleEntityCacheMisses", Unit.Calls);


		private class CacheEntry
		{
			public IRepository RepositoryEx { get; }

			public Boolean InUse { get; set; }

			public CacheEntry(IRepository repositoryEx)
			{
				RepositoryEx = repositoryEx;
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
#if NETSTANDARD2_0_OR_GREATER
#else
			_cachePolicy = new CacheItemPolicy()
			{
				SlidingExpiration = TimeSpan.FromMinutes(10),
				RemovedCallback = RemovedCallback
			};
#endif
		}

		private Int32 _isGettingCache = 0;

		/// <inheritdoc />
		IAggregateCachedRepository<TAggregate> IAggregateCachedRepositoryFactory.Create<TAggregate>(EventStoreIdentity id)
		{
			if (!_cacheDisabled)
			{
				//lock is necessary because only if a lock is aquired we can be 100% sure that a single thread is accessing
				//cached repository and entity.
				CacheEntry cached = Cache.Get(id.AsString()) as CacheEntry;

				IRepository innerRepository;

				if (cached != null)
				{
					if (cached.InUse)
					{
						//you are violating the contract of this class, you are trying to access the same aggregate from two different thread.
						//This special NStore exception will trigger a retry, so we do not block the command.
						Cache.Remove(id.AsString());
						throw new ConcurrencyException($"Single aggregate repository violation for aggregate {id}, cache is still used by another thread");
					}
					JarvisFrameworkMetricsHelper.Counter.Increment(CacheHitCounter);
					innerRepository = cached.RepositoryEx;
				}
				else
				{
					//I have nothing in cache, I'll create a new repository and since aggregate is 
					//null it will load the entity for the first time.
					JarvisFrameworkMetricsHelper.Counter.Increment(CacheMissCounter);
					innerRepository = _repositoryFactory.Create();
					cached = new CacheEntry(innerRepository);
					//element must be added to the cache for further reuse.
#if NETSTANDARD2_0_OR_GREATER
					Cache.Set(id.AsString(), cached, TimeSpan.FromMinutes(10));
#else
					Cache.Add(id.AsString(), cached, _cachePolicy);
#endif
				}

				cached.InUse = true;

				return new CachableAggregateCachedRepository<TAggregate>(
					innerRepository,
					DisposeRepository,
					AfterSave,
					OnException,
					id);
			}
			return new SingleUseAggregateCachedRepository<TAggregate>(_repositoryFactory, id);
		}

		private void DisposeRepository(String id, IRepository repository)
		{
			//do nothing
		}

		private void AfterSave(String id, IRepository repositoryEx)
		{
			//do nothing.
		}

		private void OnException(String id, IRepository repositoryEx)
		{
			//remove the repository from cache, this will really dispose wrapped repository
			_repositoryFactory.Release(repositoryEx);
			Cache.Remove(id);
		}

		void IAggregateCachedRepositoryFactory.Release<TAggregate>(IAggregateCachedRepository<TAggregate> repository)
		{
			//ok we need to verify if the repository is still in cache, if it is in cache remove InUse value.
			CacheEntry cached = Cache.Get(repository.Aggregate.Id) as CacheEntry;
			if (cached != null)
			{
				//element is still in cache can be reused so mark it as not in used anymore to release the guard.
				cached.InUse = false;
			}
		}

#if NETSTANDARD2_0_OR_GREATER
#else
		private void RemovedCallback(CacheEntryRemovedArguments arguments)
		{
			var cacheEntry = arguments.CacheItem.Value as CacheEntry;
			if (cacheEntry != null && cacheEntry.RepositoryEx != null && !cacheEntry.InUse)
			{
				//cache is not in use we can release from CASTLE
				_repositoryFactory.Release(cacheEntry.RepositoryEx);
			}
		}
#endif
	}
}

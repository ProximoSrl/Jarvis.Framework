using System;
using System.Runtime.Caching;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using Metrics;
using NEventStore;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public class CachedSnapshotManager : ISnapshotManager
    {
        private readonly ISnapshotPersister _persister;

        private readonly MemoryCache _cache;

        private readonly Boolean _cacheEnabled;

        private readonly CacheItemPolicy _standardCachePolicy;

        private readonly ISnapshotPersistenceStrategy _snapshotPersistenceStrategy;

        private static readonly Counter CacheHitCounter = Metric.Counter("CachedSnapshotManagerHits", Unit.Calls);
        private static readonly Counter CacheMissCounter = Metric.Counter("CachedSnapshotManagerMisses", Unit.Calls);

        public CachedSnapshotManager(
            ISnapshotPersister persister, 
            ISnapshotPersistenceStrategy snapshotPersistenceStrategy, 
            Boolean cacheEnabled = true,
            CacheItemPolicy cacheItemPolicy = null)
        {
            _cache = new MemoryCache("CachedSnapshotManager");
            _persister = persister;
            _snapshotPersistenceStrategy = snapshotPersistenceStrategy;
            _standardCachePolicy = cacheItemPolicy ?? new CacheItemPolicy()
            {
                SlidingExpiration = TimeSpan.FromMinutes(10)
            };
            _cacheEnabled = cacheEnabled;
        }

        public ISnapshot Load(string streamId, int upToVersion, Type aggregateType)
        {
            //Verify in cache without bothering persistence layer.
            var snapshot = _cache.Get(streamId) as ISnapshot;
            //if is in cache and version is not greater than version requested you can return
            if (_cacheEnabled && snapshot != null && snapshot.StreamRevision <= upToVersion)
            {
                CacheHitCounter.Increment();
                return snapshot;
            }
            CacheMissCounter.Increment();

            //Try load from persistence medium
            var persistedSnapshot = _persister.Load(streamId, upToVersion, aggregateType.FullName);
            if (_cacheEnabled && persistedSnapshot != null && upToVersion == Int32.MaxValue)
            {
                _cache.Set(streamId, persistedSnapshot, _standardCachePolicy);
            }
            return persistedSnapshot;
        }

        public void Snapshot(IAggregateEx aggregate, String bucket, Int32 numberOfEventsSaved)
        {
            if (SnapshotsSettings.HasOptedOut(aggregate.GetType()))
                return;

            var memento = aggregate.GetSnapshot();
            var snapshot = new Snapshot(bucket, aggregate.Id.AsString(), aggregate.Version, memento);

            if (_cacheEnabled)
            {
                _cache.Set(aggregate.Id.AsString(), snapshot, _standardCachePolicy);
            }
            
            if (_snapshotPersistenceStrategy.ShouldSnapshot(aggregate, numberOfEventsSaved))
            {
                _persister.Persist(snapshot, aggregate.GetType().FullName);
            }
        }

		public void Clear(string streamId, Type aggregateType)
		{
			if (_cacheEnabled)
				_cache.Remove(streamId);
			_persister.Clear(streamId, Int32.MaxValue, aggregateType.FullName);
		}
	}
}
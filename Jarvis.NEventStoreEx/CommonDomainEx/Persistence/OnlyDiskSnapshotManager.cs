using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using NEventStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    /// <summary>
    /// Snapshot manager that does not use in memory cache, this
    /// will prevent any problem that can arise if states are not
    /// deep cloned correctly.
    /// </summary>
    public class OnlyDiskSnapshotManager : ISnapshotManager
    {
        private readonly ISnapshotPersister _persister;

        private readonly ISnapshotPersistenceStrategy _snapshotPersistenceStrategy;

        public OnlyDiskSnapshotManager(
            ISnapshotPersister persister,
            ISnapshotPersistenceStrategy snapshotPersistenceStrategy)
        {
            _persister = persister;
            _snapshotPersistenceStrategy = snapshotPersistenceStrategy;
        }

        public ISnapshot Load(string streamId, int upToVersion, Type aggregateType)
        {
            return _persister.Load(streamId, upToVersion, aggregateType.FullName);
        }

        public void Snapshot(IAggregateEx aggregate, String bucket, Int32 numberOfEventsSaved)
        {
            if (SnapshotsSettings.HasOptedOut(aggregate.GetType()))
                return;

            var memento = aggregate.GetSnapshot();
            var snapshot = new Snapshot(bucket, aggregate.Id.AsString(), aggregate.Version, memento);
            if (_snapshotPersistenceStrategy.ShouldSnapshot(aggregate, numberOfEventsSaved))
            {
                _persister.Persist(snapshot, aggregate.GetType().FullName);
            }
        }
    }

}

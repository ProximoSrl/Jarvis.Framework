using System;
using NStore.Aggregates;
using NStore.SnapshotStore;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public class NullSnapshotManager : ISnapshotManager
    {
        public static readonly NullSnapshotManager Instance;

        static NullSnapshotManager()
        {
            Instance = new NullSnapshotManager();
        }

		public void Clear(string streamId, Type aggregateType)
        {
            // Method intentionally left empty.
        }

        public SnapshotInfo Load(string streamId, int upToVersion, Type aggregateType)
        {
            return null;
        }

        public void Snapshot(IAggregate aggregate, String bucket, Int32 numberOfEventsSaved)
        {
            // Method intentionally left empty.
        }
    }
}
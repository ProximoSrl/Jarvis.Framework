using System;
using NEventStore;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public class NullSnapshotManager : ISnapshotManager
    {
        public static readonly NullSnapshotManager Instance;

        static NullSnapshotManager()
        {
            Instance = new NullSnapshotManager();
        }

        public ISnapshot Load(string streamId, int upToVersion, Type aggregateType)
        {
            return null;
        }

        public void Snapshot(IAggregateEx aggregate, String bucket, Int32 numberOfEventsSaved)
        {

        }
    }
}
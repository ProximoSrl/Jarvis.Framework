using System;
using NStore.SnapshotStore;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public class NullSnapshotPersister : ISnapshotPersister
    {
        public static readonly NullSnapshotPersister Instance;

        static NullSnapshotPersister()
        {
            Instance = new NullSnapshotPersister();
        }

        public void Clear(string streamId, Int32 versionUpTo, String type)
        {
            // Method intentionally left empty.
        }

        public SnapshotInfo Load(string streamId, Int32 versionUpTo, String type)
        {
            return null;
        }

        public void Persist(SnapshotInfo snapshot, String type)
        {
            // Method intentionally left empty.
        }
    }
}
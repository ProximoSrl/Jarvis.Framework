using System;
using NEventStore;

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

        }

        public ISnapshot Load(string streamId, Int32 versionUpTo, String type)
        {
            return null;
        }

        public void Persist(ISnapshot snapshot, String type)
        {

        }
    }
}
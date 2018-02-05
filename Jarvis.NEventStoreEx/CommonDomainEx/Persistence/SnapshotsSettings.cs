using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public static class SnapshotsSettings
    {
        private static readonly HashSet<Type> SnapshotOptOut = new HashSet<Type>();

        static SnapshotsSettings()
        {
        }

        public static void OptOut(Type type)
        {
            SnapshotOptOut.Add(type);
        }

        public static void ClearOptOut()
        {
            SnapshotOptOut.Clear();
        }

        public static bool HasOptedOut(Type type)
        {
            return SnapshotOptOut.Contains(type);
        }
    }
}

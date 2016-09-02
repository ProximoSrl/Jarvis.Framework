using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public class NullSnapshotPersistenceStrategy : ISnapshotPersistenceStrategy
    {
        public bool ShouldSnapshot(IAggregateEx aggregate, int numberOfEventsSaved)
        {
            return false;
        }
    }
}

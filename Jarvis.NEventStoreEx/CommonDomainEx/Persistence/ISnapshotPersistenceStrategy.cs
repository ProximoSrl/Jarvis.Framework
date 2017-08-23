using NStore.Aggregates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    /// <summary>
    /// Strategy to persist a snapshot.
    /// </summary>
    public interface ISnapshotPersistenceStrategy
    {
        /// <summary>
        /// This method should return a boolean telling if the aggregate should be snapshot.
        /// </summary>
        /// <param name="aggregate">The aggregate we want to persist.</param>
        /// <param name="numberOfEventsSaved">This is the number of number of events saved with the
        /// last operation. It is needed to implement the basic strategy that rely on a snapshot each 
        /// X events.</param>
        /// <returns></returns>
        Boolean ShouldSnapshot(IAggregate aggregate, Int32 numberOfEventsSaved);
    }
}

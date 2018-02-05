using NStore.Aggregates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    /// <summary>
    /// Implements a snapshot strategy that create a snapshot each X events.
    /// </summary>
    public class NumberOfCommitsShapshotPersistenceStrategy : ISnapshotPersistenceStrategy
    {
        private readonly Int32 _commitsThreshold;

        public NumberOfCommitsShapshotPersistenceStrategy(int commitsThreshold)
        {
            _commitsThreshold = commitsThreshold;
        }

        public bool ShouldSnapshot(IAggregate aggregate, int numberOfEventsSaved)
        {
            //we need to save the aggregate if one of the event number are a multiple of the commit threshold.
            var actualVersion = aggregate.Version;
            var oldVersion = actualVersion - numberOfEventsSaved;

            for (int i = oldVersion + 1; i <= actualVersion; i++)
            {
                if (i%_commitsThreshold == 0) return true;
            }
            return false;
        }
    }
}

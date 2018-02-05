using NStore.Aggregates;
using NStore.SnapshotStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public interface ISnapshotManager
    {
        /// <summary>
        /// Take a snapshot if necessary for the aggregate.
        /// </summary>
        /// <param name="aggregate">The aggregate to snapshot</param>
        /// <param name="bucket">Bucket the aggregate belongs to</param>
        /// <param name="numberOfEventsSaved">This method is called after repository saved the aggregate
        /// and this value represents the number of events saved with the last operation. </param>
        void Snapshot(IAggregate aggregate, String bucket, Int32 numberOfEventsSaved);

        /// <summary>
        /// Retrieve a snapshot for an aggregate
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="upToVersion">The maximum version of the snapshot to load. If
        /// Int32.MaxValue is used it means that the caller needs the most recent snapshot.</param>
        /// <param name="aggregateType">Type of the aggregate to load</param>
        /// <returns></returns>
        SnapshotInfo Load(String streamId, Int32 upToVersion, Type aggregateType);

		/// <summary>
		/// Clear all snapshot cache for a stream
		/// </summary>
		/// <param name="streamId"></param>
		/// <param name="aggregateType"></param>
		void Clear(String streamId, Type aggregateType);
	}
}

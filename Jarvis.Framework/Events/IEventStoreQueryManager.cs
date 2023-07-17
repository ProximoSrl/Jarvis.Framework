using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Events
{
    /// <summary>
    /// Sometimes we need to do some specific query to the evenstore
    /// that should pass directly with mongo because the standard
    /// client of NeventStore has not the required capabilities.
    /// </summary>
    public interface IEventStoreQueryManager
    {
        /// <summary>
        /// Direct query the eventstore, this will avoid calling code to know the name of the database
        /// or the collection, but simply issue a query into the stream collection. Please remember taht
        /// if you do not filter for partition id or some other filters queries can be very slow.
        /// This query returns at max 50 chunks, no more to avoid performance issues.
        /// </summary>
        /// <param name="filter">Required: Filter to perform to the store</param>
        /// <param name="projection">Projection: can be null if you want the entire chunk</param>
        /// <param name="sort">Sort order, it can be null if you do not require ordering.</param>
        /// <returns>Maximum 50 elements from the stream.</returns>
        Task<List<BsonDocument>> DirectQueryStore(
            FilterDefinition<BsonDocument> filter,
            ProjectionDefinition<BsonDocument> projection,
            SortDefinition<BsonDocument> sort);

        /// <summary>
        /// Retrieve all commits for a specific list of aggregates created
        /// after a given checkpoint token. Its primarly use is for offline
        /// system, to retrieve conflicting messages.
        /// </summary>
        /// <param name="checkpointTokenFrom"></param>
        /// <param name="streamIds"></param>
        /// <returns></returns>
        Task<List<CommitShortInfo>> GetCommitsAfterCheckpointTokenAsync(
            Int64 checkpointTokenFrom,
            IEnumerable<String> streamIds);

 
    }
}

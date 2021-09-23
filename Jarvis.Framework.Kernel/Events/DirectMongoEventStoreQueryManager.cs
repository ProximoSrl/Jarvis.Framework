using Jarvis.Framework.Kernel.Engine;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Events
{
    public class DirectMongoEventStoreQueryManager : IEventStoreQueryManager
    {
        private readonly IMongoCollection<BsonDocument> _collection;

        public DirectMongoEventStoreQueryManager(IMongoDatabase eventsDb)
        {
            _collection = eventsDb.GetCollection<BsonDocument>(EventStoreFactory.PartitionCollectionName);
        }

        public async Task<List<CommitShortInfo>> GetCommitsAfterCheckpointTokenAsync(long checkpointTokenFrom, IEnumerable<string> streamIds)
        {
            using (var cursor = await _collection.FindAsync(
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Gt("_id", checkpointTokenFrom),
                    Builders<BsonDocument>.Filter.In("PartitionId", streamIds)
                ),
                new FindOptions<BsonDocument>()
                {
                    Projection = Builders<BsonDocument>.Projection
                        .Include("_id")
                        .Include("PartitionId")
                        .Include("OperationId")
                        .Include("Payload.Headers")
                }).ConfigureAwait(false))
            {
                return cursor
                    .ToEnumerable()
                    .Select(d => BsonSerializer.Deserialize<CommitShortInfo>(d))
                    .ToList();
            }
        }
    }
}

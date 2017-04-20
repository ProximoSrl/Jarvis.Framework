using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        /// Retrieve all commits for a specific list of aggregates created
        /// after a given checkpoint token. Its primarly use is for offline
        /// system, to retrieve conflicting messages.
        /// </summary>
        /// <param name="checkpointTokenFrom"></param>
        /// <param name="streamIds"></param>
        /// <returns></returns>
        List<CommitShortInfo> GetCommitsAfterCheckpointToken(
            Int64 checkpointTokenFrom, 
            List<String> streamIds);
    }

    public class DirectMongoEventStoreQueryManager : IEventStoreQueryManager
    {
        private readonly IMongoCollection<BsonDocument> _collection;

        public DirectMongoEventStoreQueryManager(IMongoDatabase eventsDb)
        {
            _collection = eventsDb.GetCollection<BsonDocument>("Commits");
        }

        public List<CommitShortInfo> GetCommitsAfterCheckpointToken(long checkpointTokenFrom, List<string> streamIds)
        {
            var query = _collection.Find(
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Gt("_id", checkpointTokenFrom),
                    Builders<BsonDocument>.Filter.In("StreamId", streamIds)
                ))
                .Project(
                    Builders<BsonDocument>.Projection
                        .Include("_id")
                        .Include("StreamId")
                        .Include("CommitId")
                        .Include("BucketId")
                        .Include("Headers")
                );

            return query
                .ToEnumerable()
                .Select(d => BsonSerializer.Deserialize< CommitShortInfo>(d))
                .ToList();
        }
    }

    public class CommitShortInfo
    {
        public Int64 Id { get; private set; }

        public Int64 CheckpointToken { get { return Id; } }

        public Guid CommitId { get; private set; }

        public String StreamId { get; private set; }

        public String BucketId { get; private set; }

        [BsonDictionaryOptions(Representation = DictionaryRepresentation.ArrayOfArrays)]
        public IDictionary<string, string> Headers { get; set; }
    }
}

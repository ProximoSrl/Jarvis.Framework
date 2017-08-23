using Jarvis.Framework.Kernel.Engine;
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
    public class CommitShortInfo
    {
        //public CommitShortInfo(BsonDocument document)
        //{
        //    Id = document["_id"].AsInt64;
        //    PartitionId = document["PartitionId"].AsString;
        //    OperationId = document["OperationId"].AsString;

        //    var payload = document["Payload"].AsBsonDocument;
        //    var headers = payload["Headers"].AsBsonDocument;

        //    Headers = BsonSerializer.Deserialize<Dictionary<String, Object>>(headers);
        //}

        public Int64 Id { get; private set; }

        public String PartitionId { get; private set; }

        public String OperationId { get; private set; }

        public Dictionary<String, Object> Headers => Payload?.Headers;

        /// <summary>
        /// Used for deserialization, we only need headers of the payload but headers
        /// are now in the payload and not in the outer commit (chunk)
        /// </summary>
        [BsonElement]
        private InnerPayload Payload { get; set; }

        /// <summary>
        /// Simple class to deserialize only the headers.
        /// </summary>
        [BsonIgnoreExtraElements()]
        private class InnerPayload
        {
            [BsonDictionaryOptions(Representation = DictionaryRepresentation.ArrayOfArrays)]
            public Dictionary<string, Object> Headers { get; set; }
        }
    }
}

﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Kernel.Events
{
    public class CommitShortInfo
    {
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

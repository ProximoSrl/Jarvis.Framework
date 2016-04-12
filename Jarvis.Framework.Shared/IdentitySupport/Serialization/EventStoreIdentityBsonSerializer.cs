using System;
using Jarvis.NEventStoreEx.CommonDomainEx;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Jarvis.Framework.Shared.IdentitySupport.Serialization
{
    public class EventStoreIdentityBsonSerializer : SerializerBase<EventStoreIdentity>
    {
        public static IIdentityConverter IdentityConverter { get; set; }

        public override EventStoreIdentity Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }

            var id = context.Reader.ReadString();

            if (IdentityConverter == null)
                throw new Exception("Identity converter not set in EventStoreIdentityBsonSerializer");

            return (EventStoreIdentity) IdentityConverter.ToIdentity(id);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, EventStoreIdentity value)
        {
            if (value == null)
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteString(value);
            }
        }
    }
}

using System;
using Jarvis.NEventStoreEx.CommonDomainEx;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Jarvis.Framework.Shared.IdentitySupport.Serialization
{
    public class EventStoreIdentityBsonSerializer : IBsonSerializer
    {
        public static IIdentityConverter IdentityConverter { get; set; }


        public Type ValueType
        {
            get
            {
                return typeof(IIdentity);
            }
        }

        public object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }

            var id = context.Reader.ReadString();

            if (IdentityConverter == null)
                throw new Exception("Identity converter not set in EventStoreIdentityBsonSerializer");

            return IdentityConverter.ToIdentity(id);
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            if (value == null)
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteString((EventStoreIdentity)value);
            }
        }


    }
}

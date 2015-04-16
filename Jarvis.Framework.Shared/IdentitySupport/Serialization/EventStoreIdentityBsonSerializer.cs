using System;
using Jarvis.NEventStoreEx.CommonDomainEx;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Jarvis.Framework.Shared.IdentitySupport.Serialization
{
    public class EventStoreIdentityBsonSerializer: IBsonSerializer 
    {
        public static IIdentityConverter IdentityConverter { get; set; }

        public object Deserialize(BsonReader bsonReader, Type nominalType, IBsonSerializationOptions options)
        {
            throw new NotImplementedException();
        }

        public object Deserialize(BsonReader bsonReader, Type nominalType, Type actualType, IBsonSerializationOptions options)
        {
            if (bsonReader.CurrentBsonType == BsonType.Null)
            {
                bsonReader.ReadNull();
                return null;
            }

            var id = bsonReader.ReadString();

            if (IdentityConverter == null)
                throw new Exception("Identity converter not set in EventStoreIdentityBsonSerializer");

            return IdentityConverter.ToIdentity(id);
        }

        public IBsonSerializationOptions GetDefaultSerializationOptions()
        {
            throw new NotImplementedException();
        }

        public void Serialize(BsonWriter bsonWriter, Type nominalType, object value, IBsonSerializationOptions options)
        {
            if (value == null)
            {
                bsonWriter.WriteNull();
            }
            else
            {
                bsonWriter.WriteString((EventStoreIdentity)value);
            }
        }
    }
}

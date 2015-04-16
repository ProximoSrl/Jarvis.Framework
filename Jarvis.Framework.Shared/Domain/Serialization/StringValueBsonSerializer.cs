using System;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueBsonSerializer : IBsonSerializer
    {
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
            return Activator.CreateInstance(nominalType, new object[] {id});
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
                bsonWriter.WriteString((StringValue)value);
            }
        }
    }
}

using System;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueBsonSerializer : IBsonSerializer
    {
        public Type ValueType
        {
            get
            {
                return typeof(String);
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
            return Activator.CreateInstance(args.NominalType, new object[] {id});
        }


        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            if (value == null)
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteString((StringValue)value);
            }
        }
    }
}

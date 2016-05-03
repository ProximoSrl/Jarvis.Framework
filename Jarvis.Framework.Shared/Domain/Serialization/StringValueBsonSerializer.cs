using System;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueBsonSerializer : IBsonSerializer
    {
        Type _t;
        public StringValueBsonSerializer(Type t)
        {
            _t = t;
        }
        public Type ValueType
        {
            get
            {
                return _t;
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
            StringValue sValue = value as StringValue;
            if (sValue == null || sValue.IsValid() == false)
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteString(sValue);
            }
        }
    }

    public class StringValueBsonSerializer<T> : StringValueBsonSerializer
    {
        public StringValueBsonSerializer() : base(typeof(T))
        {

        }
    }

    public class TypedStringValueBsonSerializer<T> : SerializerBase<T> where T : StringValue
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
        {
            if (value == null || value.IsValid() == false)
            {
                context.Writer.WriteNull();
            }
            else
            {
                String stringValue = (StringValue)value;
                context.Writer.WriteString(stringValue);
            }
        }

        public override T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }

            var id = context.Reader.ReadString();
            return (T)Activator.CreateInstance(args.NominalType, new object[] { id });
        }


    }

}

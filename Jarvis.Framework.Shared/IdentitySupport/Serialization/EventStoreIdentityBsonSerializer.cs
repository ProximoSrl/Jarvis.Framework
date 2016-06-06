using System;
using Jarvis.NEventStoreEx.CommonDomainEx;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.IdentitySupport.Serialization
{
    public static class MongoFlatIdSerializerHelper
    {
        internal static IIdentityConverter IdentityConverter { get; set; }

        public static void Initialize(IIdentityConverter converter)
        {
            MongoFlatIdSerializerHelper.IdentityConverter = converter;
        }

        public static T ToIdentity<T>(String id)
        {
            if (MongoFlatIdSerializerHelper.IdentityConverter == null)
                throw new Exception("Identity converter not set in MongoFlatIdSerializerHelper.IdentityConverter property");

            return (T)MongoFlatIdSerializerHelper.IdentityConverter.ToIdentity(id);
        }
    }

    public class GenericIdentityBsonSerializer : SerializerBase<IIdentity>
    {
        public override IIdentity Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }

            var id = context.Reader.ReadString();

            return MongoFlatIdSerializerHelper.ToIdentity<EventStoreIdentity>(id);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, IIdentity value)
        {
            if (value == null)
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteString(value.AsString());
            }
        }
    }

    public class TypedEventStoreIdentityBsonSerializer<T> : SerializerBase<T> where T : EventStoreIdentity
    {

        public override T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }

            var id = context.Reader.ReadString();

            return MongoFlatIdSerializerHelper.ToIdentity<T>(id);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
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

    public class IdentityArrayBsonSerializer<T> : SerializerBase<T[]> where T : IIdentity
    {
        public override T[] Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }

            List<T> retValue = new List<T>();

            context.Reader.ReadStartArray();
            
            while (context.Reader.ReadBsonType() == BsonType.String)
            {
                var id = context.Reader.ReadString();
                retValue.Add(MongoFlatIdSerializerHelper.ToIdentity<T>(id));
            }

            context.Reader.ReadEndArray();
            return retValue.ToArray();
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T[] value)
        {
            if (value == null)
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteStartArray();
                foreach (var item in value)
                {
                    context.Writer.WriteString(item.AsString());
                }
                context.Writer.WriteEndArray();
            }
        }
    }
}

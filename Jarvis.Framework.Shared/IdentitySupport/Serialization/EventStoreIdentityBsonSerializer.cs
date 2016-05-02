using System;
using Jarvis.NEventStoreEx.CommonDomainEx;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Collections.Generic;

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

            return (EventStoreIdentity)IdentityConverter.ToIdentity(id);
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

            if (EventStoreIdentityBsonSerializer.IdentityConverter == null)
                throw new Exception("Identity converter not set in EventStoreIdentityBsonSerializer");

            return (T)EventStoreIdentityBsonSerializer.IdentityConverter.ToIdentity(id);
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
            if (EventStoreIdentityBsonSerializer.IdentityConverter == null)
                throw new Exception("Identity converter not set in AuthIdentityBsonSerializer");

            context.Reader.ReadStartArray();
            
            while (context.Reader.ReadBsonType() == BsonType.String)
            {
                var id = context.Reader.ReadString();
                retValue.Add((T)EventStoreIdentityBsonSerializer.IdentityConverter.ToIdentity(id));
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

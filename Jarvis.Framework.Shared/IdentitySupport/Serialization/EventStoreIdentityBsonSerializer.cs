using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;
using System.Collections.Concurrent;
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
                throw new JarvisFrameworkIdentityException("Identity converter not set in MongoFlatIdSerializerHelper.IdentityConverter property");

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
                context.Writer.WriteString(value.AsString());
            }
        }
    }

    public class AbstractIdentityBsonSerializer<T, TId> : SerializerBase<T> where T : AbstractIdentity<TId>
    {
        public override T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return default(T);
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
                context.Writer.WriteString(String.Format("{0}_{1}", value.GetTag(), value.Id));
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

#pragma warning disable S2436 // Classes and methods should not have too many generic parameters
    public class DictionaryAsObjectJarvisSerializer<TDictionary, TKey, TValue> : DictionarySerializerBase<TDictionary, TKey, TValue>
#pragma warning restore S2436 // Classes and methods should not have too many generic parameters
        where TDictionary : class, IDictionary<TKey, TValue>
    {
        private static IBsonSerializer<TKey> GetKeySerializer()
        {
            return BsonSerializer.LookupSerializer<TKey>();
        }

        private static IBsonSerializer<TValue> GetValueSerializer()
        {
            return new ValueClassSerializer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DictionaryInterfaceImplementerSerializer{TDictionary, TKey, TValue}"/> class.
        /// </summary>
        public DictionaryAsObjectJarvisSerializer()
            : base(
                    MongoDB.Bson.Serialization.Options.DictionaryRepresentation.Document,
                    GetKeySerializer(),
                    GetValueSerializer())
        {
        }

        [Obsolete]
        protected override TDictionary CreateInstance()
        {
            return Activator.CreateInstance<TDictionary>();
        }

        private class ValueClassSerializer : IBsonSerializer<TValue>
        {
            private readonly IBsonSerializer<TValue> _base;

            public ValueClassSerializer()
            {
                _base = BsonSerializer.LookupSerializer<TValue>();
            }
            public Type ValueType
            {
                get
                {
                    return typeof(TValue);
                }
            }

            public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
            {
                IBsonSerializer specificSerializer = GetSpecificSerializer(value.GetType());
                if (specificSerializer != null)
                {
                    specificSerializer.Serialize(context, args, value);
                }
                else
                {
                    _base.Serialize(context, args, value);
                }
            }

            public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TValue value)
            {
                IBsonSerializer specificSerializer = GetSpecificSerializer(value.GetType());
                if (specificSerializer != null)
                {
                    specificSerializer.Serialize(context, args, value);
                }
                else
                {
                    _base.Serialize(context, args, value);
                }
            }

            private static readonly ConcurrentDictionary<Type, IBsonSerializer> _serializers =
                new ConcurrentDictionary<Type, IBsonSerializer>();

            private static IBsonSerializer GetSpecificSerializer(Type value)
            {
                IBsonSerializer serializer;
                if (_serializers.TryGetValue(value, out serializer))
                    return serializer;

                serializer = BsonSerializer.LookupSerializer(value);
                _serializers[value] = serializer;
                return serializer;
            }

            public TValue Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                return _base.Deserialize(context, args);
            }

            object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                return _base.Deserialize(context, args);
            }
        }
    }
}

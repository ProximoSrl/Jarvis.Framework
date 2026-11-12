using Fasterflect;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueBsonSerializer : IBsonSerializer
    {
        private static readonly ConcurrentDictionary<Type, Func<string, object>> _activators
            = new ConcurrentDictionary<Type, Func<string, object>>();

        public StringValueBsonSerializer(Type t)
        {
            ValueType = t;
        }

        public Type ValueType { get; }

        public object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }

            var id = context.Reader.ReadString();
            Func<string, object> activator;
            if (!_activators.TryGetValue(args.NominalType, out activator))
            {
                var ctor = args.NominalType.Constructor(new Type[] { typeof(string) });
                activator = FastReflectionHelper.GetActivator<string, object>(ctor);
                _activators[args.NominalType] = activator;
            }
            return activator(id);
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            StringValue sValue = value as StringValue;
            if (sValue == null || !sValue.IsValid())
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteString(sValue);
            }
        }
    }

    public class TypedStringValueBsonSerializer<T> : SerializerBase<T> where T : StringValue
    {
        /// <summary>
        /// Activator for T compiled once (one for each closed generic type): avoids the
        /// reflection based Activator.CreateInstance on every value read. It is null when T has
        /// no public constructor with a single string argument.
        /// </summary>
        private static readonly Lazy<Func<string, T>> _activator = new Lazy<Func<string, T>>(CreateActivator);

        private static Func<string, T> CreateActivator()
        {
            // Same lookup of Activator.CreateInstance: public instance constructors only.
            var ctor = typeof(T).IsAbstract ? null : typeof(T).GetConstructor(new Type[] { typeof(string) });
            return ctor == null ? null : FastReflectionHelper.GetActivator<string, T>(ctor);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
        {
            if (value == null || !value.IsValid())
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
            if (args.NominalType == typeof(T))
            {
                var activator = _activator.Value;
                if (activator != null)
                {
                    return activator(id);
                }
            }

            // Different nominal type or no suitable constructor: keep the reflection based path.
            return (T)Activator.CreateInstance(args.NominalType, new object[] { id });
        }
    }
}

using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Fasterflect;
using Jarvis.Framework.Shared.Helpers;
using System.Collections.Concurrent;
using MongoDB.Bson.Serialization.Serializers;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
	public class StringValueBsonSerializer : IBsonSerializer
	{
		private static readonly ConcurrentDictionary<Type, FastReflectionHelper.ObjectActivator> _activators
			= new ConcurrentDictionary<Type, FastReflectionHelper.ObjectActivator>();

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
			FastReflectionHelper.ObjectActivator activator;
			if (!_activators.TryGetValue(args.NominalType, out activator))
			{
				var ctor = args.NominalType.Constructor(new Type[] { typeof(string) });
				activator = FastReflectionHelper.GetActivator(ctor);
				_activators[args.NominalType] = activator;
			}
			return activator(new object[] { id });
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
			return (T)Activator.CreateInstance(args.NominalType, new object[] { id });
		}
	}
}

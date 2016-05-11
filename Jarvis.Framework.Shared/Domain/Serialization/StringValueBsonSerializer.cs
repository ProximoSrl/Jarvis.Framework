using System;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using Fasterflect;
using Jarvis.Framework.Shared.Helpers;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueBsonSerializer : IBsonSerializer
    {
        private static ConcurrentDictionary<Type, FastReflectionHelper.ObjectActivator> _activators 
            = new ConcurrentDictionary<Type, FastReflectionHelper.ObjectActivator>();

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
            FastReflectionHelper.ObjectActivator activator;
            if (!_activators.TryGetValue(nominalType, out activator))
            {
                var ctor = nominalType.Constructor(new Type[] { typeof(string) });
                activator = FastReflectionHelper.GetActivator(ctor);
                _activators[nominalType] = activator;
            }
            return activator(new object[] { id });
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

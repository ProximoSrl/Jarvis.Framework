using MongoDB.Bson;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.IdentitySupport.Serialization
{
    public class EventStoreIdentityCustomBsonTypeMapper : ICustomBsonTypeMapper
    {
        private static HashSet<Type> _registrations = new HashSet<Type>();
        public bool TryMapToBsonValue(object value, out BsonValue bsonValue)
        {
            if (value is EventStoreIdentity)
            {
                bsonValue = new BsonString((EventStoreIdentity)value);
                return true;
            }

            bsonValue = null;
            return false;
        }

        public static void Register<T>() where T : EventStoreIdentity
        {
            Type type = typeof(T);
            Register(type);
        }

        public static void Register(Type type)
        {
            if (_registrations.Contains(type))
                return;

            BsonTypeMapper.RegisterCustomTypeMapper(
                type,
                new EventStoreIdentityCustomBsonTypeMapper()
            );

            _registrations.Add(type);
        }
    }
}
using System;
using System.Collections.Generic;
using MongoDB.Bson;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueCustomBsonTypeMapper : ICustomBsonTypeMapper
    {
        private static HashSet<Type> _registrations = new HashSet<Type>();
        public bool TryMapToBsonValue(object value, out BsonValue bsonValue)
        {
            if (value is StringValue)
            {
                bsonValue = new BsonString((StringValue)value);
                return true;
            }

            bsonValue = null;
            return false;
        }

        public static void Register<T>() where T : StringValue
        {
            Type type = typeof(T);
            if (_registrations.Contains(type))
                return;

            BsonTypeMapper.RegisterCustomTypeMapper(
                type,
                new StringValueCustomBsonTypeMapper()
            );

            _registrations.Add(type);
        }
    }
}
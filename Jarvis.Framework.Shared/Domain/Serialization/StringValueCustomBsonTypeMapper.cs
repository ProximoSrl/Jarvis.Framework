﻿using MongoDB.Bson;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueCustomBsonTypeMapper : ICustomBsonTypeMapper
    {
        private static readonly HashSet<Type> _registrations = new HashSet<Type>();

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
            Register(type);
        }

        public static void Register(Type type)
        {
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
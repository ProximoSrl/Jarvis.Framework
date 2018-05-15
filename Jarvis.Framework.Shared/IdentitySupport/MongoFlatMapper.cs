using Jarvis.Framework.Shared.Domain;
using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using MongoDB.Bson.Serialization;
using System;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public static class MongoFlatMapper
    {
        private static Boolean _enabled = false;

        public static void EnableFlatMapping()
        {
            EnableFlatMapping(true);
        }

        public static void EnableFlatMapping(Boolean enableForAllId)
        {
            if (_enabled) return;

            if (enableForAllId)
            {
                BsonSerializer.RegisterSerializationProvider(new EventStoreIdentitySerializationProvider());
                BsonSerializer.RegisterSerializationProvider(new StringValueSerializationProvider());
            }

            _enabled = true;
        }
    }

    /// <summary>
    /// Class that provides the ability to autodetermine the right serializer for all 
    /// identity to enable the FlatId approach.
    /// </summary>
    public class EventStoreIdentitySerializationProvider : IBsonSerializationProvider
    {
        public IBsonSerializer GetSerializer(Type type)
        {
            if (typeof(EventStoreIdentity).IsAssignableFrom(type))
            {
                var serializerGeneric = typeof(TypedEventStoreIdentityBsonSerializer<>);
                var serializerType = serializerGeneric.MakeGenericType(type);
                var serializer = (IBsonSerializer)Activator.CreateInstance(serializerType);

                return serializer;
            }
            else if (IsSubclassOfRawGeneric(typeof(AbstractIdentity<>), type))
            {
                var serializerGeneric = typeof(AbstractIdentityBsonSerializer<,>);
                var idType = GetGenericIdTypeFromAbstractIdentity(type);
                var serializerType = serializerGeneric.MakeGenericType(type, idType);
                var serializer = (IBsonSerializer)Activator.CreateInstance(serializerType);

                return serializer;
            }
            else if (typeof(IIdentity).IsAssignableFrom(type))
            {
                return new GenericIdentityBsonSerializer();
            }

            return null;
        }

        private Type GetGenericIdTypeFromAbstractIdentity(Type type)
        {
            while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AbstractIdentity<>))
                type = type.BaseType;

            return type.GetGenericArguments()[0];
        }

        private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }
    }

    public class StringValueSerializationProvider : IBsonSerializationProvider
    {
        public IBsonSerializer GetSerializer(Type type)
        {
            if (typeof(StringValue).IsAssignableFrom(type) && !type.IsAbstract)
            {
                var serializerStringGeneric = typeof(TypedStringValueBsonSerializer<>);

                var serializerType = serializerStringGeneric.MakeGenericType(type);
                var serializer = (IBsonSerializer)Activator.CreateInstance(serializerType);
                return serializer;
            }

            return null;
        }
    }
}

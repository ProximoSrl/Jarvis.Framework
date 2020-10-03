using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Store;
using Jarvis.Framework.Shared.Support;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;
using NStore.Core.Snapshots;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jarvis.Framework.Kernel.Support
{
    public static class MongoRegistration
    {
        public static void ConfigureMongoForJarvisFramework(params String[] protectedAssemblies)
        {
            var guidConversion = new ConventionPack();
            guidConversion.Add(new GuidAsStringRepresentationConvention(protectedAssemblies.ToList()));
            ConventionRegistry.Register("guidstring", guidConversion, t => true);

            //Register everything inside the NStore that need to have custom persistence rule.
            if (!BsonClassMap.IsClassMapRegistered(typeof(Changeset)))
            {
                BsonClassMap.RegisterClassMap<Changeset>(map =>
                {
                    map.AutoMap();
                    var dictionarySerializer = new DictionaryInterfaceImplementerSerializer<Dictionary<String, Object>>(DictionaryRepresentation.ArrayOfArrays);
                    map.MapProperty(c => c.Headers).SetSerializer(dictionarySerializer);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(SnapshotInfo)))
            {
                BsonClassMap.RegisterClassMap<SnapshotInfo>(map =>
                {
                    map.AutoMap();
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(MessageReaction)))
            {
                BsonClassMap.RegisterClassMap<MessageReaction>(map =>
                {
                    map.AutoMap();
                });
            }
        }

        #region Custom class map

        private class AliasClassMap : BsonClassMap
        {
            public AliasClassMap(Type classType)
                : base(classType)
            {
                AutoMap();
                var version = 1;

                var alias = (VersionInfoAttribute)Attribute.GetCustomAttribute(classType, typeof(VersionInfoAttribute));
                var name = classType.Name;

                if (name.EndsWith("Command"))
                {
                    name = name.Remove(name.Length - "Command".Length);
                }
                else if (name.EndsWith("Event"))
                {
                    name = name.Remove(name.Length - "Event".Length);
                }

                if (alias != null)
                {
                    version = alias.Version;
                    if (!string.IsNullOrWhiteSpace(alias.Name))
                    {
                        name = alias.Name;
                    }
                }

                SetDiscriminator(String.Format("{0}_{1}", name, version));
            }
        }

        /// <summary>
        /// This class was originally created by Alessandro Giorgetti
        /// </summary>
        public class GenericClassMap : BsonClassMap
        {
            public GenericClassMap(Type classType) : base(classType)
            {
                // if it's a non generic class use the class name
                var name = classType.Name;
                if (classType.IsGenericType)
                {
                    // we create a custom concatenation of strings
                    // something like: 'AggregateSnapshot[[StateTypeName]]'
                    name = name.Substring(0, name.Length - 2); // remove the ending '1 or '2 etc... in the type name
                    var firstGenericTypeName = classType.GetGenericArguments().First().Name;
                    name += "[[" + firstGenericTypeName + "]]";
                }
                AutoMap();
                SetDiscriminator(name);
            }
        }

        private class PocoClassMap : BsonClassMap
        {
            public PocoClassMap(Type classType)
                : base(classType)
            {
                AutoMap();
            }
        }

        #endregion

        private static void RegisterTypes(IEnumerable<Type> types, bool useAlias = true)
        {
            if (useAlias)
            {
                foreach (var t in types)
                {
                    if (!BsonClassMap.IsClassMapRegistered(t))
                    {
                        BsonClassMap.RegisterClassMap(new AliasClassMap(t));
                    }
                }
            }
            else
            {
                foreach (var t in types)
                {
                    BsonClassMap.LookupClassMap(t);
                }
            }

            //now we need to map all possible messages for saga ScheduledAt
            var scheduledAtGeneric = typeof(ScheduledAt<>);
            RegisterAllInstanceOfGenericType(types, scheduledAtGeneric);
            var messageAndTimeoutGeneric = typeof(MessageAndTimeout<>);
            RegisterAllInstanceOfGenericType(types, messageAndTimeoutGeneric);
        }

        private static void RegisterAllInstanceOfGenericType(IEnumerable<Type> types, Type genericType)
        {
            foreach (var t in types)
            {
                var scheduledAt = genericType.MakeGenericType(t);
                if (!BsonClassMap.IsClassMapRegistered(scheduledAt))
                    BsonClassMap.RegisterClassMap(new GenericClassMap(scheduledAt));
            }
        }

        /// <summary>
        /// Scan the assembly to find all types that needs special registration.
        /// </summary>
        /// <param name="assembly"></param>
        public static void RegisterAssembly(Assembly assembly)
        {
            RegisterMessages(assembly);
            RegisterAggregateSnapshot(assembly);
            RegisterPlainObject(assembly);
            RegisterAllRegistrator(assembly);
        }

        private static void RegisterAllRegistrator(Assembly assembly)
        {
            var allRegistrators = assembly.GetTypes().Where(x =>
                    x.IsClass
                    && !x.IsAbstract
                    && typeof(IMongoMappingRegistrator).IsAssignableFrom(x)
               ).ToArray();

            foreach (var registratorType in allRegistrators)
            {
                var registrator = (IMongoMappingRegistrator)Activator.CreateInstance(registratorType);
                registrator.Register();
            }
        }

        private static void RegisterMessages(Assembly assembly)
        {
            var allMessages = assembly.GetTypes().Where(x =>
                    x.IsClass
                    && !x.IsAbstract
                    && typeof(IMessage).IsAssignableFrom(x)
               ).ToArray();

            RegisterTypes(allMessages);
        }

#pragma warning disable S125
        private static void RegisterAggregateSnapshot(Assembly assembly)
        {
            var allAggregateStatesObjects = assembly.GetTypes().Where(x =>
                x.IsClass
                && !x.IsAbstract
                && typeof(JarvisAggregateState).IsAssignableFrom(x)
            ).ToArray();

            RegisterTypes(allAggregateStatesObjects);

            var allEntityStatesObjects = assembly.GetTypes().Where(x =>
                x.IsClass
                && !x.IsAbstract
                && typeof(JarvisEntityState).IsAssignableFrom(x)
            ).ToArray();

            RegisterTypes(allEntityStatesObjects);
        }
#pragma warning restore S125 // Sections of code should not be "commented out"

        private static void RegisterPlainObject(Assembly assembly)
        {
            var allPocoObjectsPersistedInMongoDatabase = assembly.GetTypes().Where(x =>
                    x.IsClass
                    && !x.IsAbstract
                    && typeof(IMongoPersistedPocoObject).IsAssignableFrom(x)
               ).ToArray();

            foreach (var pocoObject in allPocoObjectsPersistedInMongoDatabase)
            {
                if (!BsonClassMap.IsClassMapRegistered(pocoObject))
                {
                    BsonClassMap.RegisterClassMap(new PocoClassMap(pocoObject));
                }
            }
        }
    }

    /// <summary>
    /// A convention that allows you to set the serialization representation of guid to a simple string
    /// </summary>
    public class GuidAsStringRepresentationConvention : ConventionBase, IMemberMapConvention
    {
        private readonly List<string> protectedAssemblies;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="GuidAsStringRepresentationConvention" /> class.
        /// </summary>  
        /// <param name="protectedAssemblies"></param>
        public GuidAsStringRepresentationConvention(List<string> protectedAssemblies)
        {
            this.protectedAssemblies = protectedAssemblies;
        }

        /// <summary>
        /// Applies a modification to the member map.
        /// </summary>
        /// <param name="memberMap">The member map.</param>
        public void Apply(BsonMemberMap memberMap)
        {
            var memberTypeInfo = memberMap.MemberType.GetTypeInfo();
            if (memberTypeInfo == typeof(Guid))
            {
                var declaringTypeAssembly = memberMap.ClassMap.ClassType.Assembly;
                var asmName = declaringTypeAssembly.GetName().Name;
                if (protectedAssemblies.Any(a => a.Equals(asmName, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                var serializer = memberMap.GetSerializer();
                var representationConfigurableSerializer = serializer as IRepresentationConfigurable;
                if (representationConfigurableSerializer != null)
                {
                    var _representation = BsonType.String;
                    var reconfiguredSerializer = representationConfigurableSerializer.WithRepresentation(_representation);
                    memberMap.SetSerializer(reconfiguredSerializer);
                }
            }
        }
    }
}
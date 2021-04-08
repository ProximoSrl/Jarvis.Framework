using Fasterflect;
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
            ConventionRegistry.Register("guidstring", guidConversion, _ => true);

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
                BsonClassMap.RegisterClassMap<SnapshotInfo>(map => map.AutoMap());
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(MessageReaction)))
            {
                BsonClassMap.RegisterClassMap<MessageReaction>(map => map.AutoMap());
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

        private static void RegisterType(Type t, bool useAlias = true)
        {
            if (useAlias)
            {
                if (!BsonClassMap.IsClassMapRegistered(t))
                {
                    BsonClassMap.RegisterClassMap(new AliasClassMap(t));
                }
            }
            else
            {
                BsonClassMap.LookupClassMap(t);
            }

            //now we need to map all possible messages for saga ScheduledAt
            var scheduledAtGeneric = typeof(ScheduledAt<>);
            RegisterAllInstanceOfGenericType(t, scheduledAtGeneric);
            var messageAndTimeoutGeneric = typeof(MessageAndTimeout<>);
            RegisterAllInstanceOfGenericType(t, messageAndTimeoutGeneric);
        }

        private static void RegisterAllInstanceOfGenericType(Type t, Type genericType)
        {
            var scheduledAt = genericType.MakeGenericType(t);
            if (!BsonClassMap.IsClassMapRegistered(scheduledAt))
            {
                BsonClassMap.RegisterClassMap(new GenericClassMap(scheduledAt));
            }
        }

        private static HashSet<Assembly> _alreadyRegisterdAssemblies = new HashSet<Assembly>();

        /// <summary>
        /// Scan the assembly to find all types that needs special registration.
        /// </summary>
        /// <param name="assembly"></param>
        public static void RegisterAssembly(Assembly assembly)
        {
            if (!_alreadyRegisterdAssemblies.Contains(assembly))
            {
                var assemblyTypes = assembly.GetTypes();
                foreach (var type in assemblyTypes)
                {
                    RegisterUpcasters(type);
                    RegisterMessages(type);
                    RegisterAggregateSnapshot(type);
                    RegisterPlainObject(type);
                }

                _alreadyRegisterdAssemblies.Add(assembly);
            }
        }

        private static void RegisterUpcasters(Type type)
        {
            if (!type.IsAbstract && typeof(IUpcaster).IsAssignableFrom(type))
            {
                var realUpcaster = (IUpcaster)type.CreateInstance();
                StaticUpcaster.RegisterUpcaster(realUpcaster);
            }
        }

        private static void RegisterMessages(Type type)
        {
            if (
                    type.IsClass
                    && !type.IsAbstract
                    && typeof(IMessage).IsAssignableFrom(type)
               )
            {
                RegisterType(type);
            }
        }

#pragma warning disable S125
        private static void RegisterAggregateSnapshot(Type type)
        {
            if (
                type.IsClass
                && !type.IsAbstract
                && (typeof(JarvisAggregateState).IsAssignableFrom(type) || typeof(JarvisEntityState).IsAssignableFrom(type))
            )
            {
                RegisterType(type);
            }
        }
#pragma warning restore S125 // Sections of code should not be "commented out"

        private static void RegisterPlainObject(Type type)
        {
            if (
                    type.IsClass
                    && !type.IsAbstract
                    && typeof(IMongoPersistedPocoObject).IsAssignableFrom(type)
                    && !BsonClassMap.IsClassMapRegistered(type))
            {
                BsonClassMap.RegisterClassMap(new PocoClassMap(type));
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
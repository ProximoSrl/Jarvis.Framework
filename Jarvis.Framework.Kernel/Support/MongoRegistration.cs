using System;
using System.Collections.Generic;
using Jarvis.Framework.Shared.Store;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;
using System.Reflection;
using System.Linq;
using Jarvis.Framework.Shared.Messages;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.IdentitySupport;

namespace Jarvis.Framework.Kernel.Support
{
    public static class MongoRegistration
    {

        public static void RegisterMongoConversions(params String[] protectedAssemblies)
        {
            var guidConversion = new ConventionPack();
            guidConversion.Add(new GuidAsStringRepresentationConvention(protectedAssemblies.ToList()));
            ConventionRegistry.Register("guidstring", guidConversion, t => true);
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
        public class AggregateSnaphotClassMap : BsonClassMap
        {
            public AggregateSnaphotClassMap(Type classType)  : base(classType)
            {
                // if it's a non generic class use the class name
                var name = classType.Name;
                if (classType.IsGenericType)
                {
                    // it it's a generic type use the first argument as discriminator
                    // name = classType.FullName; // .GetGenericArguments().First().Name cannot be the same of the class that will be stored in the 'state' property;
                    // only overwriting the name is not very good for refactoring

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

        #endregion

        private static void RegisterTypes(IEnumerable<Type> types, bool useAlias = true)
        {
            if (useAlias)
            {
                foreach (var t in types)
                {
                    if (!BsonClassMap.IsClassMapRegistered(t))
                        BsonClassMap.RegisterClassMap(new AliasClassMap(t));
                }
            }
            else
            {
                foreach (var t in types)
                {
                    BsonClassMap.LookupClassMap(t);
                }
            }
        }

        /// <summary>
        /// Scan the assembly to find all types that needs special registration.
        /// </summary>
        /// <param name="assembly"></param>
        public static void RegisterAssembly(Assembly assembly)
        {
            RegisterMessages(assembly);
            //RegisterIdentities(assembly);
            RegisterAggregateSnapshot(assembly);
        }

        private static void RegisterMessages(Assembly assembly)
        {
            var allMessages = assembly.GetTypes().Where(x =>
                                                       x.IsClass
                                                       && !x.IsAbstract
                                                       && typeof(IMessage).IsAssignableFrom(x)
               ).ToArray();


            MongoRegistration.RegisterTypes(allMessages);
        }

        //public static void RegisterIdentities(Assembly assembly)
        //{
        //    var allIdentities = assembly.GetTypes().Where(x =>
        //                                                  x.IsClass
        //                                                  && !x.IsAbstract
        //                                                  && typeof(IIdentity).IsAssignableFrom(x)
        //       ).ToArray();

        //    MongoRegistration.RegisterTypes(allIdentities, true);
        //}

        private static void RegisterAggregateSnapshot(Assembly assembly)
        {
            var allAggregateStatesObjects = assembly.GetTypes().Where(x =>
                                                          x.IsClass
                                                          && !x.IsAbstract
                                                          && typeof(AggregateState).IsAssignableFrom(x)
               ).ToArray();

            var aggregateSnapshotGeneric = typeof(AggregateSnapshot<>);
            foreach (var stateType in allAggregateStatesObjects)
            {
                var realType = aggregateSnapshotGeneric.MakeGenericType(stateType);
                BsonClassMap.RegisterClassMap(new AggregateSnaphotClassMap(realType));
            }
        }
    }

    /// <summary>
    /// A convention that allows you to set the serialization representation of guid to a simple string
    /// </summary>
    public class GuidAsStringRepresentationConvention : ConventionBase, IMemberMapConvention
    {
        private List<string> protectedAssemblies;

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
                    BsonType _representation = BsonType.String;
                    var reconfiguredSerializer = representationConfigurableSerializer.WithRepresentation(_representation);
                    memberMap.SetSerializer(reconfiguredSerializer);
                }
            }
        }
    }
}
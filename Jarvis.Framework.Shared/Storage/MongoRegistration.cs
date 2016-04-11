using System;
using System.Collections.Generic;
using Jarvis.Framework.Shared.Store;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;

namespace Jarvis.Framework.Shared.Storage
{
    public static class MongoRegistration
    {
        static MongoRegistration()
        {
            //TODO: CONVERSION
            //var conventions = new ConventionPack
            //    {
            //        new MemberSerializationOptionsConvention(
            //            typeof (Guid),
            //            new RepresentationSerializationOptions(BsonType.String)
            //            )
            //    };
            //ConventionRegistry.Register("guidstring", conventions, t => true);
        }

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


        public static void RegisterTypes(IEnumerable<Type> types, bool useAlias = true)
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

    }
}
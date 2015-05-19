using Jarvis.Framework.Shared.Domain;
using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.NEventStoreEx.CommonDomainEx;
using MongoDB.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public static class MongoFlatMapper
    {
        private static Boolean _enabled = false;

        public static void EnableFlatMapping(Boolean enableForAllId = true)
        {
            if (_enabled) return;

            BsonClassMap.RegisterClassMap<DomainEvent>(map =>
            {
                map.AutoMap();
                map.MapProperty(x => x.AggregateId).SetSerializer(new EventStoreIdentityBsonSerializer());

            });

            if (enableForAllId)
            {
                AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
                AddSerializerForAllStringBasedIdFromThisApplication();
            }

            _enabled = true;
        }

        private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            AddSerializerForAllStringBasedIdFromAssembly(args.LoadedAssembly);
        }

        public static void AddSerializerForAllStringBasedIdFromAssembly(Assembly assembly)
        {
            var asmName = assembly.FullName;
            foreach (var type in assembly.GetTypes()
                .Where(t => typeof(IIdentity).IsAssignableFrom(t)  &&
                            t.IsAbstract == false
                      ))
            {
                EventStoreIdentityCustomBsonTypeMapper.Register(type);
            }

            foreach (var type in assembly.GetTypes()
              .Where(t => typeof(LowercaseStringValue).IsAssignableFrom(t) &&
                          t.IsAbstract == false
                    ))
            {
                BsonSerializer.RegisterSerializer(type, new StringValueBsonSerializer());
            }
        }

        public static void AddSerializerForAllStringBasedIdFromThisApplication()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                AddSerializerForAllStringBasedIdFromAssembly(assembly);
            }
        }
    }
}

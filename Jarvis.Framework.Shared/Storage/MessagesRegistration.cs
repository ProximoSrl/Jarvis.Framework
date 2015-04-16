using System.Linq;
using System.Reflection;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.Shared.Storage
{
    public static class MessagesRegistration
    {
        public static void RegisterMessagesInAssembly(Assembly assembly)
        {
            var allMessages = assembly.GetTypes().Where(x =>
                                                        x.IsClass
                                                        && !x.IsAbstract
                                                        && typeof(IMessage).IsAssignableFrom(x)
                                                        && !typeof(IDomainEvent).IsAssignableFrom(x)
                                                        && !typeof(ICommand).IsAssignableFrom(x)
                ).ToArray();

            MongoRegistration.RegisterTypes(allMessages);
        }
        /*
                public static void RegisterEventsInAssembly(Assembly assembly)
                {
                    var allEvents = assembly.GetTypes().Where(x => x.IsClass && !x.IsAbstract && typeof(IDomainEvent).IsAssignableFrom(x)).ToArray();

                    MongoRegistration.RegisterTypes(allEvents);
                }
        */
        public static void RegisterAssembly(Assembly assembly)
        {
            var allMessages = assembly.GetTypes().Where(x =>
                                                       x.IsClass
                                                       && !x.IsAbstract
                                                       && typeof(IMessage).IsAssignableFrom(x)
               ).ToArray();


            MongoRegistration.RegisterTypes(allMessages);

            RegisterIdentities(assembly);
        }

        public static void RegisterIdentities(Assembly assembly)
        {
            var allIdentities = assembly.GetTypes().Where(x =>
                                                          x.IsClass
                                                          && !x.IsAbstract
                                                          && typeof(IIdentity).IsAssignableFrom(x)
               ).ToArray();

            MongoRegistration.RegisterTypes(allIdentities, true);
        }
    }
}
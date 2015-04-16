using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CQRS.Kernel.Engine;
using CQRS.Shared.Events;
using MongoDB.Bson.Serialization;

namespace CQRS.Kernel.Events
{
    public class EventsRegistration
    {
        public void RegisterEventsInAssembly(Assembly assembly)
        {
            var allEvents = assembly.GetTypes().Where(x => x.IsClass && !x.IsAbstract && typeof (IDomainEvent).IsAssignableFrom(x)).ToArray();

            foreach (var eventClass in allEvents)
            {
                BsonClassMap.LookupClassMap(eventClass);
            }
        }
    }
}

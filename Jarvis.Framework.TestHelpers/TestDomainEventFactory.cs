using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.TestHelpers
{
    public static class DomainEventTestExtensions
    {
        public static T AssignIdForTest<T>(this T evt, IIdentity id) where T : DomainEvent
        {
            evt.SetPropertyValue("AggregateId", id);
            return evt;
        }
    }
}

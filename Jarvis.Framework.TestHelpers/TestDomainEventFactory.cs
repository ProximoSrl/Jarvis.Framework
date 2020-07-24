using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.TestHelpers
{
    public static class DomainEventTestExtensions
    {
        public static T AssignIdForTest<T>(this T evt, IIdentity id) where T : DomainEvent
        {
            evt.SetPropertyValue("AggregateId", id);
            return evt;
        }

        public static T AssignIssuedByForTest<T>(this T evt, String issuedBy) where T : DomainEvent
        {
            if (evt.Context == null)
            {
                evt.SetPropertyValue("Context", new Dictionary<String, Object>());
            }

            evt.Context.Add(MessagesConstants.UserId, issuedBy);
            return evt;
        }
    }
}

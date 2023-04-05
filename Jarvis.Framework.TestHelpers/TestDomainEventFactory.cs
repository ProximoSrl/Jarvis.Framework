using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.TestHelpers
{
    public static class DomainEventTestExtensions
    {
        public static T AssignIdForTest<T>(this T evt, IIdentity id) where T : DomainEvent
        {
            evt.SetPropertyValue("AggregateId", id);
            return evt;
        }

        public static T AssignPositionValues<T>(
           this T evt,
           Int64 checkpointToken,
           Int64 aggregateVersion,
           Int32 eventPosition) where T : DomainEvent
        {
            evt.SetPropertyValue("CheckpointToken", checkpointToken);
            evt.SetPropertyValue("EventPosition", eventPosition);
            evt.SetPropertyValue("Version", aggregateVersion);

            return evt;
        }

        public static T AssignIssuedByForTest<T>(this T evt, String issuedBy) where T : DomainEvent
        {
            Dictionary<string, object> newDic;
            if (evt.Context == null)
            {
                newDic = new Dictionary<String, Object>();
            }
            else
            {
                newDic = evt.Context.ToDictionary(k => k.Key, k => k.Value);
            }

            newDic.Add(MessagesConstants.UserId, issuedBy);
            evt.SetPropertyValue(e => e.Context, newDic);
            return evt;
        }
    }
}

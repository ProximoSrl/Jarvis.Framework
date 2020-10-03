using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Jarvis.Framework.Shared.Helpers
{
    public static class NStoreExtensions
    {
        public static Int64 GetChunkPosition(this Changeset cs)
        {
            if (cs.Events.Length == 0)
                return 0; //we cannot determine position it does not contains anything.

            var evt = cs.Events[0] as DomainEvent;
            if (evt == null)
                return 0; //we cannot determine position it does not contains events.

            return evt.CheckpointToken;
        }

        public static IIdentity GetIdentity(this Changeset cs)
        {
            if (cs.Events?.Length == 0)
                return null;

            var evt = cs.Events[0] as DomainEvent;
            if (evt == null)
                return null;

            return evt.AggregateId;
        }
    }
}

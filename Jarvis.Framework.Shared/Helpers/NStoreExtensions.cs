using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Domain;
using System;

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

        /// <summary>
        /// Given a <see cref="IChunk"/> it will return a list of object representing
        /// events. This is needed because we can have payload of type <see cref="Changeset"/>
        /// or simple objects (for component that does stream processing).
        /// </summary>
        /// <param name="chunk"></param>
        /// <returns>Array of object to dispatch, never return null.</returns>
        public static object[] GetDispatchList(this IChunk chunk)
        {
            if (chunk.Payload is Changeset changeset)
            {
                return changeset.Events;
            }
            else if (chunk.Payload != null)
            {
                return new Object[] { chunk.Payload };
            }
            else
            {
                return EmptyCommitEvents;
            }
        }
    }
}

using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Domain;
using System;

namespace Jarvis.Framework.Shared.Helpers
{
    /// <summary>
    /// Extensions for Nstore
    /// </summary>
    public static class NStoreExtensions
    {
        /// <summary>
        /// Get the chunk position from a changeset.
        /// </summary>
        /// <param name="cs"></param>
        /// <returns></returns>
        public static Int64 GetChunkPosition(this Changeset cs)
        {
            if (cs.Events.Length == 0)
                return 0; //we cannot determine position it does not contains anything.

            var evt = cs.Events[0] as DomainEvent;
            if (evt == null)
                return 0; //we cannot determine position it does not contains events.

            return evt.CheckpointToken;
        }

        /// <summary>
        /// Get the identity from the changeset.
        /// </summary>
        /// <param name="cs"></param>
        /// <returns></returns>
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
        /// Get timestamp from a changeset in UTC format.
        /// </summary>
        /// <param name="cs"></param>
        /// <returns></returns>
        public static DateTime? GetTimestamp(this Changeset cs)
        {
            return ChangesetCommonHeaders.GetTimestampFromHeaders(cs.Headers);
        }
    }
}

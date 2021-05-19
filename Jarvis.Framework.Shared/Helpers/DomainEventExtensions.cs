using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Shared.Helpers
{
    public static class DomainEventExtensions
    {
        private static readonly Guid[] Empty = new Guid[0];

        /// <summary>
        /// When an event was generated offline, and its command is replayed on the 
        /// main system, new events have offline corresponding events in a special header.
        /// This function extract all the <see cref="DomainEvent.MessageId"/> of all 
        /// corresponding offline events.
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        public static Guid[] GetOfflineEventIdList(this DomainEvent evt)
        {
            if (evt.Context != null && evt.Context.ContainsKey(MessagesConstants.OfflineEvents))
            {
                var contextValue = evt.Context[MessagesConstants.OfflineEvents];

                //If header is string, it was probably serialized with command exchange from 
                //offline and online system,.
                if (contextValue is String)
                    contextValue = ((String)contextValue).DeserializeFromCommandHeader<IEnumerable<DomainEvent>>();

                if (contextValue == null || !(contextValue is IEnumerable))
                    return Empty;

                return ((IEnumerable)contextValue)
                    .OfType<DomainEvent>()
                    .Select(e => e.MessageId)
                    .ToArray();
            }
            return Empty;
        }
    }
}

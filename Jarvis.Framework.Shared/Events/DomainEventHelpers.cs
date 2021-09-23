using NStore.Domain;
using System;

namespace Jarvis.Framework.Shared.Events
{
    public static class DomainEventHelpers
    {
        public static DateTime GetTimestamp(this Changeset changeset)
        {
            var headers = changeset.Headers;
            if (headers?.ContainsKey(ChangesetCommonHeaders.Timestamp) == true)
            {
                return (headers[ChangesetCommonHeaders.Timestamp] as DateTime?) ?? DateTime.MinValue;
            }
            return DateTime.MinValue;
        }
    }
}

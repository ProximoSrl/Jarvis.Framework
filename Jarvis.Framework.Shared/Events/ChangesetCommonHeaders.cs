using Jarvis.Framework.Shared.Messages;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Events
{
    public static class ChangesetCommonHeaders
    {
        /// <summary>
        /// Timstamp of changeset generation
        /// </summary>
        public const string Timestamp = "ts";

        /// <summary>
        /// Command that generates the changeset. It is used only for diagnostic purposes.
        /// </summary>
        public const string Command = "command";

        /// <summary>
        /// Contains the logic that allows to extract timestamp from the context of the
        /// commit.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static DateTime GetTimestampFromHeaders(IReadOnlyDictionary<string, object> context) 
        {
            if (context == null) return DateTime.MinValue;

            if (context.TryGetValue(MessagesConstants.OverrideCommitTimestamp, out var timestamp))
            {
                // I have overide timestamp with date time
                if (timestamp is DateTime date)
                {
                    return date;
                }
                else if (timestamp is string dateString)
                {
                    if (DateTime.TryParse(dateString, out var parsedDate))
                    {
                        return parsedDate;
                    }
                }
            }

            if (context.TryGetValue(ChangesetCommonHeaders.Timestamp, out var tsValue)
                && tsValue is DateTime dateTimeTsValue)
            {
                return dateTimeTsValue;
            }

            return DateTime.MinValue;
        }
    }
}

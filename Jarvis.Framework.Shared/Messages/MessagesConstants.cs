using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Messages
{
	/// <summary>
	/// Static class used to identify typical headers that can be used in 
	/// the header of the command and thus replicated into the headers
	/// of the Changeset.
	/// </summary>
    public static class MessagesConstants
    {
        public const String SagaIdHeader = "sagaId";
        public const String ReplyToHeader = "reply-to";
        public const String UserId = "user.id";
        public const String CommandTimestamp = "command.timestamp";

        /// <summary>
        /// If this header is present, we shuld execute the command only
        /// if the modified aggregate is at the specified version and 
        /// not at a greater version.
        /// </summary>
        /// <remarks>This value is checked inside the RepositoryCommandHandler class, if command
        /// is executed with a different handler this header is not honored.</remarks>
        public const String IfVersionEqualsTo = "if-version-equals-to";

		/// <summary>
		/// When offline system push a command to main system to synchronize, this header
		/// will contain the list of events that were generated offline due to that command.
		/// </summary>
		public const String OfflineEvents = "offline-events";
    }
}

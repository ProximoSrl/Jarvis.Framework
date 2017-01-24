using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Messages
{
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
        /// When an offline session starts, it copied commits database up to a certain
        /// checkpoint token. To understand if we have conflicts, we need to know this value
        /// to execute the command conditionally.
        /// </summary>
        public const String SessionStartCheckpointToken = "session-start-checkpoint-token";

        /// <summary>
        /// if this header is present, the execution of the command is a "sync" execution and
        /// the command will be executed only if the aggregate has no commit greater than
        /// <see cref="MessagesConstants.SessionStartCheckpointToken"/> that does not belongs to
        /// <see cref="MessagesConstants.OfflineSessionId"/>.
        /// </summary>
        public const String SessionStartCheckEnabled = "session-start-check-enabled";

        /// <summary>
        /// This header contains the guid of the offline session that originally generates the message
        /// </summary>
        public const String OfflineSessionId = "offline-session-id";

        /// <summary>
        /// original execution timestamp of the command. 
        /// </summary>
        public const String OfflineExecutionTimestamp = "offline-execution-timestamp";

        /// <summary>
        /// Text that contains information of offline execution, it is used only to troubleshoot.
        /// </summary>
        public const String OfflineExecutionInfo = "offline-execution-info";
    }
}

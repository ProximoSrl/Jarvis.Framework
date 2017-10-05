using Castle.Core.Logging;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Logging
{
    public static class LoggingHelper
    {
        public static void MarkCommandExecution(this IExtendedLogger logger, ICommand message)
        {
            logger.ThreadProperties[LoggingConstants.CommandId] = message?.MessageId;
            logger.ThreadProperties[LoggingConstants.UserId] = message?.GetContextData(MessagesConstants.UserId);
            logger.ThreadProperties[LoggingConstants.CommandDescription] = String.Format("{0} [{1}]", message?.Describe(), message?.GetType());
        }

        public static void ClearCommandExecution(this IExtendedLogger logger)
        {
            logger.ThreadProperties[LoggingConstants.CommandId] = null;
            logger.ThreadProperties[LoggingConstants.CommandDescription] = null;
            logger.ThreadProperties[LoggingConstants.UserId] = null;
        }
    }
}

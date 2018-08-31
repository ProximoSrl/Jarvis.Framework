using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using System;

namespace Jarvis.Framework.Shared.Logging
{
    public static class LoggingHelper
    {
        /// <summary>
        /// During command execution store some specific property on all the loggers so you can 
        /// correlate logs to command execution.
        /// </summary>
        /// <param name="loggerThreadContextManager"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        public static void MarkCommandExecution(this ILoggerThreadContextManager loggerThreadContextManager, ICommand command)
        {
            loggerThreadContextManager.SetContextProperty(LoggingConstants.CommandId, command?.MessageId);
            loggerThreadContextManager.SetContextProperty(LoggingConstants.UserId, command?.GetContextData(MessagesConstants.UserId));
            loggerThreadContextManager.SetContextProperty(LoggingConstants.CommandDescription, String.Format("{0} [{1}]", command?.Describe(), command?.GetType()));

            //correlation id during command execution is given by a special context data and default to message id if correlation is not present
            var correlationId = command?.GetContextData(LoggingConstants.CorrleationId) ?? command?.MessageId.ToString();
            loggerThreadContextManager.SetContextProperty(LoggingConstants.CorrleationId, correlationId);
        }

        public static void ClearMarkCommandExecution(this ILoggerThreadContextManager loggerThreadContextManager)
        {
            loggerThreadContextManager.ClearContextProperty(LoggingConstants.CommandId);
            loggerThreadContextManager.ClearContextProperty(LoggingConstants.UserId);
            loggerThreadContextManager.ClearContextProperty(LoggingConstants.CommandDescription);

            //correlation id during command execution is given by a special context data and default to message id if correlation is not present
            loggerThreadContextManager.ClearContextProperty(LoggingConstants.CorrleationId);
        }
    }
}

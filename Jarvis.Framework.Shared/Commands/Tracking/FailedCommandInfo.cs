using System;
using Jarvis.Framework.Shared.Commands;

namespace Jarvis.Framework.Shared.Commands.Tracking
{
    public class FailedCommandInfo
    {
        public ICommand Command { get; private set; }
        public string ErrorMessage { get; private set; }
        public Exception Exception { get; private set; }

        public FailedCommandInfo(ICommand command, string errorMessage, Exception exception = null)
        {
            Command = command;
            ErrorMessage = errorMessage;
            Exception = exception;
        }
    }
}

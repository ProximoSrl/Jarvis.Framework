using Jarvis.Framework.Shared.Messages;
using System;

namespace Jarvis.Framework.Shared.Commands
{
    /// <summary>
    /// This is a generic interface used to handle exception for command execution. An interface
    /// gives us the ability to change how the exception are handled
    /// </summary>
    public interface ICommandExecutionExceptionHelper
    {
        /// <summary>
        /// Handle an exception during command execution
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="command"></param>
        /// <param name="retryCount">This is the number of retry.</param>
        /// <param name="retry">If the function return true, the exception was handled, if this value is true
        /// it signal to the caller that the execution of the command should be retried, because the exception
        /// is transient (ex: concurrency)</param>
        /// <param name="replyCommand">When retry is false, and the return value is true, the exception was handled
        /// and there is no need to retry the command. This is the <see cref="CommandHandled"/> object that should returned
        /// to the caller (ex through a bus)</param>
        /// <returns>True if the execution was handled, false if the execution cannot be handled and should be simply 
        /// rethrow</returns>
        Boolean Handle(Exception ex, ICommand command, Int32 retryCount, out Boolean retry, out CommandHandled replyCommand);
    }
}


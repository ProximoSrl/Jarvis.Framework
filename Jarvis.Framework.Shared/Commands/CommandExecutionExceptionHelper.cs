using Castle.Core.Logging;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.Support;
using NStore.Core.Streams;
using System;
using System.Security;
using System.Threading;

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

    public class JarvisDefaultCommandExecutionExceptionHelper : ICommandExecutionExceptionHelper
    {
        private readonly ILogger _logger;
        private readonly Int32 _numberOfConcurrencyExceptionBeforeRandomSleeping;
        private readonly Int32 _maxRetryOnConcurrencyException;

        public JarvisDefaultCommandExecutionExceptionHelper(
            ILogger logger,
            Int32 numberOfConcurrencyExceptionBeforeRandomSleeping,
            Int32 maxRetryOnConcurrencyException)
        {
            _logger = logger;
            if (numberOfConcurrencyExceptionBeforeRandomSleeping <= 0)
                throw new ArgumentException(
                    $"Value {numberOfConcurrencyExceptionBeforeRandomSleeping} is not valid, we should have a value greater than 0",
                    nameof(numberOfConcurrencyExceptionBeforeRandomSleeping));

            if (maxRetryOnConcurrencyException <= 0)
                throw new ArgumentException(
                    $"Value {maxRetryOnConcurrencyException} is not valid, we should have a value greater than 0",
                    nameof(maxRetryOnConcurrencyException));

            _numberOfConcurrencyExceptionBeforeRandomSleeping = numberOfConcurrencyExceptionBeforeRandomSleeping;
            _maxRetryOnConcurrencyException = maxRetryOnConcurrencyException;
        }

        public Boolean Handle(Exception ex, ICommand command, Int32 retryCount, out Boolean retry, out CommandHandled replyCommand)
        {
            retry = false;
            replyCommand = null;

            switch (ex)
            {
                case AggregateException aex:
                    aex = aex.Flatten();
                    Boolean aexRetry = false;
                    CommandHandled aexReplyCommand = null;
                    Boolean aexHandled = false;
                    aex.Handle(e => InnerHandle(e, command, retryCount, out aexRetry, out aexReplyCommand, out aexHandled));
                    //if we reach here, all exception were correctly handled.
                    retry = aexRetry;
                    replyCommand = aexReplyCommand;
                    return aexHandled;
                default:
                    Boolean handled;
                    InnerHandle(ex, command, retryCount, out retry, out replyCommand, out handled);
                    return handled;
            }
        }

        /// <summary>
        /// Inner function to handle the different type of exceptions
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="command"></param>
        /// <param name="retryCount"></param>
        /// <param name="retry"></param>
        /// <param name="replyCommand"></param>
        /// <returns>Return always true because it is the very same routine used for aggregate exception. We always want not to throw in this
        /// method, but allow the caller to throw.</returns>
        private Boolean InnerHandle(Exception ex, ICommand command, Int32 retryCount, out Boolean retry, out CommandHandled replyCommand, out Boolean shouldContinue)
        {
            retry = false;
            replyCommand = null;
            shouldContinue = false;
            switch (ex)
            {
                case ConcurrencyException cex:

                    SharedMetricsHelper.MarkConcurrencyException();
                    // retry
                    if (_logger.IsInfoEnabled) _logger.InfoFormat(ex, "Handled {0} {1} [{2}], concurrency exception. Retry count: {3}", command.GetType().FullName, command.MessageId, command.Describe(), retryCount);

                    // increment the retries counter and maybe add a delay
                    if (retryCount++ > _numberOfConcurrencyExceptionBeforeRandomSleeping)
                    {
                        Thread.Sleep(new Random(DateTime.Now.Millisecond).Next(retryCount * 10));
                    }
                    retry = retryCount < _maxRetryOnConcurrencyException; //can retry
                    shouldContinue = true; //can proceed
                    break;

                case DomainException dex:

                    SharedMetricsHelper.MarkDomainException();
                    var notifyTo = command.GetContextData(MessagesConstants.ReplyToHeader);
                    if (notifyTo != null)
                    {
                        replyCommand = new CommandHandled(
                            notifyTo,
                            command.MessageId,
                            CommandHandled.CommandResult.Failed,
                            command.Describe(),
                            ex.Message,
                            isDomainException: true
                        );
                        replyCommand.CopyHeaders(command);
                    }
                    _logger.ErrorFormat(ex, "DomainException on command {0} [MessageId: {1}] : {2} : {3}", command.GetType(), command.MessageId, command.Describe(), ex.Message);
                    shouldContinue = true; //exception was handled, can proceed to the execution but retry is false
                    break;

                case SecurityException sex:

                    SharedMetricsHelper.MarkDomainException();
                    var notifySexTo = command.GetContextData(MessagesConstants.ReplyToHeader);
                    if (notifySexTo != null)
                    {
                        replyCommand = new CommandHandled(
                            notifySexTo,
                            command.MessageId,
                            CommandHandled.CommandResult.Failed,
                            command.Describe(),
                            $"Security exception: {ex.Message}",
                            isDomainException: false
                        );
                        replyCommand.CopyHeaders(command);
                    }
                    _logger.ErrorFormat(ex, "SecurityException on command {0} [MessageId: {1}] : {2} : {3}", command.GetType(), command.MessageId, command.Describe(), ex.Message);
                    shouldContinue = true; //exception was handled, can proceed to the execution but retry is false because security should not retry
                    break;

                case Exception gex:

                    _logger.ErrorFormat(ex, "Generic Exception on command {0} [MessageId: {1}] : {2} : {3}", command.GetType(), command.MessageId, command.Describe(), ex.Message);
                    shouldContinue = false; //cannot proceed, this exception cannot be handled
                    break;
            }

            return true;
        }
    }
}


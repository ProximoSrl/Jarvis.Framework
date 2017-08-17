using System;
using System.Threading;
using Castle.Core.Logging;
using NEventStore.Domain.Persistence;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;
using Rebus;
using Jarvis.Framework.Shared.Logging;
using Metrics;
using Rebus.Handlers;
using Rebus.Bus;
using System.Threading.Tasks;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
    /// <summary>
    /// MessageHandler (Bus) to CommandHandler (Jarvis.Framework.Kernel) Adapter
    /// </summary>
    /// <typeparam name="T">CommandType</typeparam>
    public class MessageHandlerToCommandHandlerAdapter<T> : IHandleMessages<T> where T : ICommand
    {
        public IExtendedLogger Logger { get; set; }

        private readonly ICommandHandler<T> _commandHandler;
        readonly IMessagesTracker _messagesTracker;
        readonly IBus _bus;
        private readonly Int32 _numberOfConcurrencyExceptionBeforeRandomSleeping;

        public MessageHandlerToCommandHandlerAdapter(
            ICommandHandler<T> commandHandler,
            IMessagesTracker messagesTracker,
            IBus bus,
            Int32 numberOfConcurrencyExceptionBeforeRandomSleeping = 5)
        {
            Logger = NullLogger.Instance;
            _commandHandler = commandHandler;
            _messagesTracker = messagesTracker;
            _bus = bus;
            _numberOfConcurrencyExceptionBeforeRandomSleeping = numberOfConcurrencyExceptionBeforeRandomSleeping;
        }

        public async Task Handle(T message)
        {
            try
            {
                Logger.MarkCommandExecution(message);
                if (Logger.IsDebugEnabled) Logger.DebugFormat("Handling {0} {1}", message.GetType().FullName, message.MessageId);
                int i = 0;
                bool done = false;
                var notifyTo = message.GetContextData(MessagesConstants.ReplyToHeader);
                while (!done && i < 100)
                {
                    try
                    {
                        _messagesTracker.ElaborationStarted(message, DateTime.UtcNow);
                        _commandHandler.Handle(message);
                        _messagesTracker.Completed(message, DateTime.UtcNow);
                        done = true;

                        if (notifyTo != null && message.GetContextData("disable-success-reply", "false") != "true")
                        {
                            var replyCommand = new CommandHandled(
                                notifyTo,
                                message.MessageId,
                                CommandHandled.CommandResult.Handled,
                                message.Describe()
                            );
                            replyCommand.CopyHeaders(message);
                            await _bus.Reply(replyCommand).ConfigureAwait(false);
                        }
                    }
                    catch (ConflictingCommandException ex)
                    {
                        MetricsHelper.MarkConcurrencyException();
                        // retry
                        if (Logger.IsInfoEnabled) Logger.InfoFormat(ex, "Handled {0} {1} [{2}], concurrency exception. Retry count: {3}", message.GetType().FullName, message.MessageId, message.Describe(), i);
                        // increment the retries counter and maybe add a delay
                        if (i++ > _numberOfConcurrencyExceptionBeforeRandomSleeping)
                        {
                            Thread.Sleep(new Random(DateTime.Now.Millisecond).Next(i * 10));
                        }
                    }
                    catch (DomainException ex)
                    {
                        MetricsHelper.MarkDomainException();
                        done = true;
                        _messagesTracker.Failed(message, DateTime.UtcNow, ex);

                        if (notifyTo != null)
                        {
                            var replyCommand = new CommandHandled(
                                notifyTo,
                                message.MessageId,
                                CommandHandled.CommandResult.Failed,
                                message.Describe(),
                                ex.Message,
                                true
                            );
                            replyCommand.CopyHeaders(message);
                            await _bus.Reply(replyCommand).ConfigureAwait(false);
                        }
                        Logger.ErrorFormat(ex, "DomainException on command {0} [MessageId: {1}] : {2} : {3}", message.GetType(), message.MessageId, message.Describe(), ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorFormat(ex, "Generic Exception on command {0} [MessageId: {1}] : {2} : {3}", message.GetType(), message.MessageId, message.Describe(), ex.Message);
                        _messagesTracker.Failed(message, DateTime.UtcNow, ex);
                        throw; //rethrow exception.
                    }
                }
                if (!done)
                {
                    Logger.ErrorFormat("Too many conflict on command {0} [MessageId: {1}] : {2}", message.GetType(), message.MessageId, message.Describe());
                    var exception = new Exception("Command failed. Too many Conflicts");
                    _messagesTracker.Failed(message, DateTime.UtcNow, exception);
                }
                if (Logger.IsDebugEnabled) Logger.DebugFormat("Handled {0} {1} {2}", message.GetType().FullName, message.MessageId, message.Describe());
            }
            finally
            {
                Logger.ClearCommandExecution();
            }
        }
    }
}
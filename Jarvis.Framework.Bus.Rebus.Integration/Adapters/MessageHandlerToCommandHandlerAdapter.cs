using System;
using System.Threading;
using Castle.Core.Logging;
using CommonDomain.Persistence;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;
using Rebus;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
    /// <summary>
    /// MessageHandler (Bus) to CommandHandler (Jarvis.Framework.Kernel) Adapter
    /// </summary>
    /// <typeparam name="T">CommandType</typeparam>
    public class MessageHandlerToCommandHandlerAdapter<T> : IHandleMessages<T> where T : ICommand
    {
        private ILogger _logger = NullLogger.Instance;
        public ILogger Logger
        {
            get { return _logger; }
            set { _logger = value; }
        }

        private readonly ICommandHandler<T> _commandHandler;
        readonly IMessagesTracker _messagesTracker;
        readonly IBus _bus;

        public MessageHandlerToCommandHandlerAdapter(ICommandHandler<T> commandHandler, IMessagesTracker messagesTracker, IBus bus)
        {
            _commandHandler = commandHandler;
            _messagesTracker = messagesTracker;
            _bus = bus;
        }

        public void Handle(T message)
        {
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Handling {0} {1}", message.GetType().FullName, message.MessageId);
            int i = 0;
            bool done = false;
            var notifyTo = message.GetContextData(MessagesConstants.ReplyToHeader);

            while (!done && i < 100)
            {
                i++;
                try
                {
                    _commandHandler.Handle(message);
                    _messagesTracker.Completed(message.MessageId, DateTime.UtcNow);
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
                        _bus.Reply(replyCommand);
                    }
                }
                catch (ConflictingCommandException ex)
                {
                    // retry
                    if (Logger.IsDebugEnabled) Logger.DebugFormat("Handled {0} {1}, concurrency exception. Retrying", message.GetType().FullName, message.MessageId);
                    if (i++ > 5)
                    {
                        Thread.Sleep(new Random(DateTime.Now.Millisecond).Next(i * 10));
                    }
                }
                catch (DomainException ex)
                {
                    done = true;
                    _messagesTracker.Failed(message.MessageId, DateTime.UtcNow, ex);

                    if (notifyTo != null)
                    {
                        var replyCommand = new CommandHandled(
                            notifyTo,
                            message.MessageId,
                            CommandHandled.CommandResult.Failed,
                            message.Describe(),
                            ex.Message
                        );
                        replyCommand.CopyHeaders(message);
                        _bus.Reply(replyCommand);
                    }
                }
            }
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Handled {0} {1}", message.GetType().FullName, message.MessageId);
        }
    }
}
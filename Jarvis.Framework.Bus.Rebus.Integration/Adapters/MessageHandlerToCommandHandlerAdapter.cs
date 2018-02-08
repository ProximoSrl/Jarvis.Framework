using System;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.Logging;
using Rebus.Handlers;
using Rebus.Bus;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;
using NStore.Core.Streams;
using Jarvis.Framework.Shared.Exceptions;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
    /// <summary>
    /// MessageHandler (Bus) to CommandHandler (Jarvis.Framework.Kernel) Adapter
    /// </summary>
    /// <typeparam name="T">CommandType</typeparam>
    public class MessageHandlerToCommandHandlerAdapter<T> : IHandleMessages<T> where T : ICommand
	{
		public ILogger Logger { get; set; }
		public ILoggerThreadContextManager LoggerThreadContextManager { get; set; }

		private readonly ICommandHandler<T> _commandHandler;
		private readonly IMessagesTracker _messagesTracker;
		private readonly IBus _bus;
        private readonly ICommandExecutionExceptionHelper _commandExecutionExceptionHelper;

        public MessageHandlerToCommandHandlerAdapter(
			ICommandHandler<T> commandHandler,
            ICommandExecutionExceptionHelper commandExecutionExceptionHelper,
            IMessagesTracker messagesTracker,
			IBus bus)
		{
			Logger = NullLogger.Instance;
			LoggerThreadContextManager = NullLoggerThreadContextManager.Instance;
			_commandHandler = commandHandler;
			_messagesTracker = messagesTracker;
			_bus = bus;
            _commandExecutionExceptionHelper = commandExecutionExceptionHelper;
        }

		public async Task Handle(T message)
		{
			using (LoggerThreadContextManager.MarkCommandExecution(message))
			{
				if (Logger.IsDebugEnabled) Logger.DebugFormat("Handling {0} {1}", message.GetType().FullName, message.MessageId);

                int i = 0;
				bool retry = false;
				var notifyTo = message.GetContextData(MessagesConstants.ReplyToHeader);

                CommandHandled replyCommandHandled = null;
                Boolean success = false;
                Exception lastException = null;
                do
                {
                    try
                    {
                        _messagesTracker.ElaborationStarted(message, DateTime.UtcNow);
                        _commandHandler.HandleAsync(message).Wait(); //need to wait, or you will free the worker rebus thread and you will dispatch many concurrent handler.
                        _messagesTracker.Completed(message, DateTime.UtcNow);

                        if (notifyTo != null && message.GetContextData("disable-success-reply", "false") != "true")
                        {
                            replyCommandHandled = new CommandHandled(
                                notifyTo,
                                message.MessageId,
                                CommandHandled.CommandResult.Handled,
                                message.Describe()
                            );
                            replyCommandHandled.CopyHeaders(message);
                            _bus.Reply(replyCommandHandled).Wait();
                        }
                        success = true;
                        retry = false;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        if (!_commandExecutionExceptionHelper.Handle(ex, message, i, out retry, out replyCommandHandled))
                        {
                            //Handler is not able to handle the exception, simply retrhow
                            _messagesTracker.Failed(message, DateTime.UtcNow, ex);
                            throw;
                        }
                    }
                    i++; //if we reach here we need to increment the counter.

                } while (retry);

                if (!success)
				{
					_messagesTracker.Failed(message, DateTime.UtcNow, lastException);

                    if (notifyTo != null  && replyCommandHandled != null)
                    {
                        await _bus.Reply(replyCommandHandled).ConfigureAwait(false);
                    }
                }
				if (Logger.IsDebugEnabled) Logger.DebugFormat("Handled {0} {1} {2}", message.GetType().FullName, message.MessageId, message.Describe());
			}
		}
	}
}
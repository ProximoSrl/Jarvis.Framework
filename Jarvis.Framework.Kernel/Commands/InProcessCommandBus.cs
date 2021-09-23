﻿using Castle.Core.Logging;
using Castle.MicroKernel;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Commands.Tracking;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.MultitenantSupport;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Commands
{
    public interface IInProcessCommandBus : ICommandBus
    {
    }

    public abstract class InProcessCommandBus : IInProcessCommandBus
    {
        private readonly IKernel _kernel;
        private readonly ICommandExecutionExceptionHelper _commandExecutionExceptionHelper;

        public ILogger Logger { get; set; }

        private IMessagesTracker _messagesTracker { get; set; }

        public ILoggerThreadContextManager LoggerThreadContextManager { get; set; }

        protected InProcessCommandBus(
            IKernel kernel,
            IMessagesTracker messagesTracker,
            ICommandExecutionExceptionHelper commandExecutionExceptionHelper)
        {
            _kernel = kernel;
            _messagesTracker = messagesTracker;
            Logger = NullLogger.Instance;
            LoggerThreadContextManager = NullLoggerThreadContextManager.Instance;
            _commandExecutionExceptionHelper = commandExecutionExceptionHelper;
        }

        public Task<ICommand> SendAsync(ICommand command, string impersonatingUser = null)
        {
            return SendLocalAsync(command, impersonatingUser);
        }

        /// <summary>
        /// ATTENTION: deferring command with in process command bus actually does not defer anything.
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="command"></param>
        /// <param name="impersonatingUser"></param>
        /// <returns></returns>
        public Task<ICommand> DeferAsync(TimeSpan delay, ICommand command, string impersonatingUser = null)
        {
            Logger.WarnFormat("Sending command {0} without delay", command.MessageId);
            return SendLocalAsync(command, impersonatingUser);
        }

        public async Task<ICommand> SendLocalAsync(ICommand command, string impersonatingUser = null)
        {
            LoggerThreadContextManager.MarkCommandExecution(command);
            try
            {
                PrepareCommand(command, impersonatingUser);
                var msg = command as IMessage;
                if (msg != null)
                {
                    _messagesTracker.Started(msg);
                }

                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat(
                        "Sending command {0}:\n{1}",
                        command.GetType().FullName,
                        JsonConvert.SerializeObject(command, Formatting.Indented)
                    );
                }

                var handlers = ResolveHandlers(command);

                if (handlers.Length == 0)
                {
                    throw new JarvisFrameworkEngineException(string.Format("Command {0} does not have any handler", command.GetType().FullName));
                }

                if (handlers.Length > 1)
                {
                    var b = new StringBuilder();
                    b.AppendFormat("Command {0} has too many handlers\n", command.GetType().FullName);

                    foreach (ICommandHandler handler in handlers)
                    {
                        b.AppendFormat("\t{0}", handler.GetType().FullName);
                    }
                    ReleaseHandlers(handlers);
                    throw new JarvisFrameworkEngineException(b.ToString());
                }

                await HandleAsync(handlers[0], command).ConfigureAwait(false);

                ReleaseHandlers(handlers);

                return command;
            }
            finally
            {
                LoggerThreadContextManager.ClearMarkCommandExecution();
            }
        }

        protected virtual void PrepareCommand(ICommand command, string impersonatingUser = null)
        {
            var userId = command.GetContextData(MessagesConstants.UserId);

            if (userId == null)
            {
                userId = ImpersonateUser(command, impersonatingUser);
                command.SetContextData(MessagesConstants.UserId, userId);
            }

            if (!UserIsAllowedToSendCommand(command, userId))
            {
                throw new UserCannotSendCommandException();
            }
        }

        protected virtual string ImpersonateUser(
            ICommand command,
            string impersonatingUser)
        {
            return impersonatingUser ?? GetCurrentExecutingUser();
        }

        protected virtual string GetCurrentExecutingUser()
        {
            return null;
        }

        protected abstract bool UserIsAllowedToSendCommand(ICommand command, string userName);

        protected virtual async Task HandleAsync(ICommandHandler handler, ICommand command)
        {
            int i = 0;
            Boolean success = false;
            Boolean retry = false;
            CommandHandled replyCommandHandled = null;
            Exception lastException = null;
            do
            {
                try
                {
                    _messagesTracker.ElaborationStarted(command, DateTime.UtcNow);
                    await handler.HandleAsync(command).ConfigureAwait(false);
                    _messagesTracker.Completed(command, DateTime.UtcNow);
                    success = true;
                    retry = false;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (!_commandExecutionExceptionHelper.Handle(ex, command, i, out retry, out replyCommandHandled))
                    {
                        //Handler is not able to handle the exception, simply retrhow
                        _messagesTracker.Failed(command, DateTime.UtcNow, ex);
                        throw;
                    }
                }
                i++; //if we reach here we need to increment the counter.
            } while (retry);

            if (!success)
            {
                _messagesTracker.Failed(command, DateTime.UtcNow, lastException);
                throw lastException;
            }
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Command {0} executed: {1}", command.MessageId, command.Describe());
        }

        protected virtual void ReleaseHandlers(ICommandHandler[] handlers)
        {
            foreach (ICommandHandler handler in handlers)
            {
                _kernel.ReleaseComponent(handler);
            }
        }

        protected virtual ICommandHandler[] ResolveHandlers(ICommand command)
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(new[] { command.GetType() });
            return _kernel.ResolveAll(handlerType).Cast<ICommandHandler>().ToArray();
        }
    }

    public abstract class MultiTenantInProcessCommandBus : InProcessCommandBus
    {
        private readonly ITenantAccessor _tenantAccessor;

        protected MultiTenantInProcessCommandBus(
            ITenantAccessor tenantAccessor,
            IKernel kernel,
            IMessagesTracker messagesTracker,
            ICommandExecutionExceptionHelper commandExecutionExceptionHelper)
            : base(kernel, messagesTracker, commandExecutionExceptionHelper)
        {
            _tenantAccessor = tenantAccessor;
        }

        protected override void ReleaseHandlers(ICommandHandler[] handlers)
        {
            var currentTenant = _tenantAccessor.Current;
            if (currentTenant != null)
            {
                foreach (var handler in handlers)
                {
                    currentTenant.Container.Release(handler);
                }
            }
        }

        protected override ICommandHandler[] ResolveHandlers(ICommand command)
        {
            var currentTenant = _tenantAccessor.Current;
            if (currentTenant != null)
            {
                var handlerType = typeof(ICommandHandler<>).MakeGenericType(new[] { command.GetType() });
                return currentTenant.Container.ResolveAll(handlerType).Cast<ICommandHandler>().ToArray();
            }

            return new ICommandHandler[0];
        }
    }
}

using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using Castle.MicroKernel;
using NEventStore.Domain.Persistence;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.MultitenantSupport;
using Newtonsoft.Json;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Logging;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

namespace Jarvis.Framework.Kernel.Commands
{
    public class UserCannotSendCommandException : Exception
    {
    }

    public interface IInProcessCommandBus : ICommandBus
    {
    }

    public abstract class InProcessCommandBus : IInProcessCommandBus
    {
        private readonly IKernel _kernel;
        private IExtendedLogger _logger = NullLogger.Instance;

        public IExtendedLogger Logger
        {
            get { return _logger; }
            set { _logger = value; }
        }

        private IMessagesTracker _messagesTracker { get; set; }

        protected InProcessCommandBus(IMessagesTracker messagesTracker)
        {
            _messagesTracker = messagesTracker;
        }

        public InProcessCommandBus(IKernel kernel, IMessagesTracker messagesTracker)
            : this(messagesTracker)
        {
            _kernel = kernel;
        }

        public ICommand Send(ICommand command, string impersonatingUser = null)
        {
            return SendLocal(command, impersonatingUser);
        }

        public ICommand Defer(TimeSpan delay, ICommand command, string impersonatingUser = null)
        {
            Logger.WarnFormat("Sending command {0} without delay", command.MessageId);
            return SendLocal(command, impersonatingUser);
        }

        public ICommand SendLocal(ICommand command, string impersonatingUser = null)
        {
            try
            {
                Logger.MarkCommandExecution(command);
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
                    throw new Exception(string.Format("Command {0} does not have any handler", command.GetType().FullName));
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
                    throw new Exception(b.ToString());
                }

                Handle(handlers[0], command);

                ReleaseHandlers(handlers);

                return command;
            }
            finally
            {
                Logger.ClearCommandExecution();
            }
        }

        protected virtual void PrepareCommand(ICommand command, string impersonatingUser = null)
        {
            var userId = command.GetContextData("user.id");

            if (userId == null)
            {
                userId = ImpersonateUser(command, impersonatingUser);
                command.SetContextData("user.id", userId);
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

        protected virtual void Handle(ICommandHandler handler, ICommand command)
        {
            int i = 0;
            bool done = false;

            while (!done && i < 100)
            {
                try
                {
                    _messagesTracker.ElaborationStarted(command.MessageId, DateTime.UtcNow);
                    handler.Handle(command);
                    _messagesTracker.Completed(command.MessageId, DateTime.UtcNow);
                    done = true;
                }
                catch (ConflictingCommandException ex)
                {
                    MetricsHelper.MarkConcurrencyException();
                    // retry
                    if (Logger.IsInfoEnabled) Logger.InfoFormat(ex, "Handled {0} {1}, concurrency exception. Retrying", command.GetType().FullName, command.MessageId);
                    if (i++ > 5)
                    {
                        Thread.Sleep(new Random(DateTime.Now.Millisecond).Next(i * 10));
                    }
                }
                catch (DomainException ex)
                {
                    _logger.ErrorFormat(ex, "DomainException on command {0} [MessageId: {1}] : {2} : {3}", command.GetType(), command.MessageId, command.Describe(), ex.Message);
                    MetricsHelper.MarkDomainException();
                    _messagesTracker.Failed(command.MessageId, DateTime.UtcNow, ex);
                    throw; //rethrow domain exception.
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "Generic Exception on command {0} [MessageId: {1}] : {2} : {3}", command.GetType(), command.MessageId, command.Describe(), ex.Message);
                    _messagesTracker.Failed(command.MessageId, DateTime.UtcNow, ex);
                    throw; //rethrow exception.
                }
            }
            if (done == false)
            {
                _logger.ErrorFormat("Too many conflict on command {0} [MessageId: {1}] : {2}", command.GetType(), command.MessageId, command.Describe());
                var exception = new Exception("Command failed. Too many Conflicts");
                _messagesTracker.Failed(command.MessageId, DateTime.UtcNow, exception);

                throw exception;
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
            var handlers = _kernel.ResolveAll(handlerType).Cast<ICommandHandler>().ToArray();
            return handlers;
        }
    }

    public abstract class MultiTenantInProcessCommandBus : InProcessCommandBus
    {
        readonly ITenantAccessor _tenantAccessor;

        public MultiTenantInProcessCommandBus(ITenantAccessor tenantAccessor, IMessagesTracker messagesTracker)
            : base(messagesTracker)
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

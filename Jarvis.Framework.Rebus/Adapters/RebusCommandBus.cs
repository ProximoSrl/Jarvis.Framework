using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Messages;
using Rebus.Bus;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
    public abstract class RebusCommandBus : ICommandBus
    {
        private readonly IBus _bus;

        public ILogger Logger { get; set; }

        protected RebusCommandBus(IBus bus)
        {
            _bus = bus;
            Logger = NullLogger.Instance;
        }

        public async Task<ICommand> SendAsync(ICommand command, string impersonatingUser = null)
        {
            PrepareCommand(command, impersonatingUser);
            await SendCommand(command).ConfigureAwait(false);
            return command;
        }

        private Task SendCommand(ICommand command)
        {
            var forcedDispatchQueue = command.GetContextData(MessagesConstants.DestinationAddress);
            if (!String.IsNullOrEmpty(forcedDispatchQueue))
            {
                return _bus.Advanced.Routing.Send(forcedDispatchQueue, command);
            }
            else
            {
                return _bus.Send(command);
            }
        }

        public async Task<ICommand> SendLocalAsync(ICommand command, string impersonatingUser = null)
        {
            PrepareCommand(command, impersonatingUser);
            await SendCommandLocal(command).ConfigureAwait(false);
            return command;
        }

        private Task SendCommandLocal(ICommand command)
        {
            var forcedDispatchQueue = command.GetContextData(MessagesConstants.DestinationAddress);
            if (!String.IsNullOrEmpty(forcedDispatchQueue))
            {
                throw new JarvisFrameworkEngineException("Cannot use a DestinationAddress with a send local. It is allowed only with a standard send.");
            }
            else
            {
                return _bus.SendLocal(command);
            }
        }

        public async Task<ICommand> DeferAsync(TimeSpan delay, ICommand command, string impersonatingUser = null)
        {
            PrepareCommand(command, impersonatingUser);

            if (delay <= TimeSpan.Zero)
            {
                await SendCommand(command).ConfigureAwait(false);
            }
            else
            {
                var forcedDispatchQueue = command.GetContextData(MessagesConstants.DestinationAddress);
                if (!String.IsNullOrEmpty(forcedDispatchQueue))
                {
                    throw new JarvisFrameworkEngineException("Cannot use a DestinationAddress with a deferred message. It is allowed only with a standard send.");
                }
                await _bus.Defer(delay, command).ConfigureAwait(false);
            }

            return command;
        }

        protected abstract bool UserIsAllowedToSendCommand(ICommand command, string userName);

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

        protected virtual void PrepareCommand(ICommand command, string impersonatingUser)
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
    }
}
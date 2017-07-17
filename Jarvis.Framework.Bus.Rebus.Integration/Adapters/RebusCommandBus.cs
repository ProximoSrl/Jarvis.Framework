using System;
using System.Security.Claims;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Shared.Commands;
using Rebus;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Messages;

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

        public ICommand Send(ICommand command, string impersonatingUser = null)
        {
            PrepareCommand(command, impersonatingUser);
            _bus.Send(command);
            return command;
        }

        public ICommand SendLocal(ICommand command, string impersonatingUser = null)
        {
            PrepareCommand(command, impersonatingUser);
            _bus.SendLocal(command);
            return command;
        }

        public ICommand Defer(TimeSpan delay, ICommand command, string impersonatingUser = null)
        {
            PrepareCommand(command, impersonatingUser);

            if (delay <= TimeSpan.Zero)
            {
                _bus.Send(command);
            }
            else
            {
                _bus.Defer(delay, command);
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

		    foreach (var key in command.AllContextKeys)
            {
                _bus.AttachHeader(command,key, command.GetContextData(key));
            }

            _bus.AttachHeader(command, "command.id", command.MessageId.ToString());
            _bus.AttachHeader(command, "command.description", command.Describe());
        }
    }
}
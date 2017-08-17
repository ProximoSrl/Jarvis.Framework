using System;
using System.Security.Claims;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Shared.Commands;
using Rebus;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Messages;
using Rebus.Bus;
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

        public async Task<ICommand> Send(ICommand command, string impersonatingUser = null)
        {
            PrepareCommand(command, impersonatingUser);
            await _bus.Send(command).ConfigureAwait(false);
            return command;
        }

        public async Task<ICommand> SendLocal(ICommand command, string impersonatingUser = null)
        {
            PrepareCommand(command, impersonatingUser);
            await _bus.SendLocal(command).ConfigureAwait(false);
            return command;
        }

        public async Task<ICommand> Defer(TimeSpan delay, ICommand command, string impersonatingUser = null)
        {
            PrepareCommand(command, impersonatingUser);

            if (delay <= TimeSpan.Zero)
            {
                await _bus.Send(command).ConfigureAwait(false);
            }
            else
            {
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

            //TODOREBUS: No more attach header ... 
		    //foreach (var key in command.AllContextKeys)
      //      {
      //          _bus.attach(command,key, command.GetContextData(key));
      //      }

      //      _bus.AttachHeader(command, "command.id", command.MessageId.ToString());
      //      _bus.AttachHeader(command, "command.description", command.Describe());
        }
    }
}
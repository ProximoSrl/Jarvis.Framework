using System;
using System.Security.Claims;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Shared.Commands;
using Rebus;
using Castle.Core.Logging;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
	public class RebusCommandBus : ICommandBus
    {
        private IBus _bus;

        public ILogger Logger { get; set; }

        public RebusCommandBus(IBus bus)
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

	    protected virtual bool UserIsAllowedToSendCommand(ICommand command, string userName)
	    {
		    return true;
	    }

	    protected virtual void PrepareCommand(ICommand command, string impersonatingUser)
        {
            var userId = command.GetContextData("user.id");

            if (userId == null)
            {
                userId = impersonatingUser ?? ClaimsPrincipal.Current.Identity.Name;

                if (String.IsNullOrWhiteSpace(userId))
                    throw new Exception("Missing user id information in command context");

                command.SetContextData("user.id", userId);
            }

		    if (userId != "import" &&  !UserIsAllowedToSendCommand(command, userId))
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
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Messages;
using Rebus.Bus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
	public class RebusMessageBus : IMessageBus
	{
		private readonly IBus _bus;

		public ILogger Logger { get; set; }

		public RebusMessageBus(IBus bus)
		{
			_bus = bus;
			Logger = NullLogger.Instance;
		}

		public async Task<IMessage> SendAsync(IMessage message)
		{
			await _bus.Send(message).ConfigureAwait(false);
			return message;
		}

		public async Task<IMessage> SendLocalAsync(IMessage message)
		{
			await _bus.SendLocal(message).ConfigureAwait(false);
			return message;
		}

		public async Task<IMessage> DeferAsync(TimeSpan delay, IMessage message)
		{
			if (delay <= TimeSpan.Zero)
			{
				await _bus.Send(message).ConfigureAwait(false);
			}
			else
			{
				await _bus.Defer(delay, message).ConfigureAwait(false);
			}
			return message;
		}
	}
}

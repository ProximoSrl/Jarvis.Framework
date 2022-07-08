using Castle.Core.Logging;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using Rebus.Bus;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Rebus.Adapters
{
    /// <summary>
    /// This message bus is used when someone (like process manager) should dispatch
    /// standard Messages in the bus, not something that inherits from <see cref="ICommand"/>
    /// </summary>
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

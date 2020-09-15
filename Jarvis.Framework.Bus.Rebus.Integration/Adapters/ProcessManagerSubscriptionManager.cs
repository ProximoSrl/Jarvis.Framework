using Castle.MicroKernel;
using Jarvis.Framework.Kernel.Engine;
using Rebus.Bus;

/* Unmerged change from project 'Jarvis.Framework.Bus.Rebus.Integration (net461)'
Before:
using System.Linq;
using Rebus.Bus;
using Rebus.Handlers;
After:
using System.Bus;
using Rebus.Handlers;
using System;
using System.Collections.Generic;
*/
using Rebus.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
    /// <summary>
    /// This will scan all process manager listener, and all objects that is registered
    /// in Castle with the interface IHandleMessages and explicitly subcribes them
    /// in rebus.
    /// </summary>
    public class ProcessManagerSubscriptionManager
    {
        private readonly IProcessManagerListener[] _listeners;
        private readonly IBus _bus;
        private readonly IKernel _kernel;

        public ProcessManagerSubscriptionManager(IProcessManagerListener[] listeners, IBus bus, IKernel kernel)
        {
            _listeners = listeners;
            _bus = bus;
            _kernel = kernel;
        }

        public void SubscribeSync()
        {
            SubscribeAsync().Wait();
        }

        public async Task SubscribeAsync()
        {
            var subscribeToMessages = new HashSet<Type>();
            foreach (var listener in _listeners)
            {
                foreach (var message in listener.ListeningTo)
                {
                    subscribeToMessages.Add(message);
                }
            }

            //then should scan Castle for each component that registered IHandleMessage
            foreach (var handler in _kernel.GetAssignableHandlers(typeof(Object)))
            {
                var messageHandlers = handler.ComponentModel.Services
                    .Where(s => s.IsGenericType && s.GetGenericTypeDefinition() == typeof(IHandleMessages<>));
                foreach (var handlerType in messageHandlers)
                {
                    subscribeToMessages.Add(handlerType.GenericTypeArguments[0]);
                }
            }

            foreach (var type in subscribeToMessages)
            {
                // subscription
                await _bus.Subscribe(type).ConfigureAwait(false);
            }
        }
    }
}

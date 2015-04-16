using System;
using System.Collections.Generic;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Jarvis.Framework.Kernel.Engine;
using Rebus;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
    public class ListenerRegistration 
    {
        readonly IProcessManagerListener[] _listeners;
        readonly IBus _bus;
        readonly IKernel _kernel;

        public ListenerRegistration(IProcessManagerListener[] listeners, IBus bus, IKernel kernel)
        {
            _listeners = listeners;
            _bus = bus;
            _kernel = kernel;
        }

        public void Subscribe()
        {
            var subscriber = _bus.GetType().GetMethod("Subscribe");
            var subscribeToMessages = new HashSet<Type>();
            foreach (var listener in _listeners)
            {
                var processManagerType = listener.GetProcessType();
                foreach (var message in listener.ListeningTo)
                {
                    subscribeToMessages.Add(message);

                    // adapter
                    var handlerType = typeof(IHandleMessages<>).MakeGenericType(message);
                    var handlerImpl = typeof(RebusSagaAdapter<,>).MakeGenericType(processManagerType, message);
                    
                    _kernel.Register(
                        Component
                            .For(handlerType)
                            .ImplementedBy(handlerImpl)
                            .LifeStyle.Transient
                    );
                }
            }

            foreach (var type in subscribeToMessages)
            {
                // subscription
                var subscription = subscriber.MakeGenericMethod(new[] { type });
                subscription.Invoke(_bus, new object[] { });
            }
        }
    }
}

using Castle.MicroKernel;
using Fasterflect;
using Jarvis.Framework.Shared.Events;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Kernel.Events
{
    public class InProcessEventBus : IEventBus
    {
        protected IKernel Kernel { get; set; }
        private readonly ConcurrentDictionary<string, MethodInvoker> _cache = new ConcurrentDictionary<string, MethodInvoker>();

        public InProcessEventBus(IKernel kernel)
        {
            Kernel = kernel;
        }

        public void Publish(DomainEvent e)
        {
            var eventType = e.GetType();
            var serviceType = typeof(IEventHandler<>).MakeGenericType(eventType);
            //            using (Kernel.BeginScope())
            {
                var services = Kernel.ResolveAll(serviceType);
                foreach (var service in services)
                {
                    var serviceImpType = service.GetType();
                    string key = serviceImpType.FullName + "|" + eventType.FullName;
                    var invoker = _cache.GetOrAdd(key, s => serviceImpType.DelegateForCallMethod("On", Flags.InstancePublic, new[] { eventType }));

                    invoker.Invoke(service, e);
                }
            }
        }
    }
}

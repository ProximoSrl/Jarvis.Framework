using Jarvis.Framework.Shared.Events;

namespace Jarvis.Framework.Kernel.Events
{
    public interface IEventBus
    {
        void Publish(DomainEvent e);
    }
}
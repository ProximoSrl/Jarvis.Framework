using System.Collections;

namespace Jarvis.NEventStoreEx.CommonDomainEx
{
    public interface IAggregateEx
    {
        IIdentity Id { get; }
        int Version { get; }

        void ApplyEvent(object @event);
        ICollection GetUncommittedEvents();
        void ClearUncommittedEvents();

        IMementoEx GetSnapshot();
    }
}
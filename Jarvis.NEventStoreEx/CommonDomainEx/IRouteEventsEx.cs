using System;

namespace Jarvis.NEventStoreEx.CommonDomainEx
{
    public interface IRouteEventsEx
    {
        void Register<T>(Action<T> handler);

        void Register(IAggregateEx aggregate);

        void Dispatch(object eventMessage);
    }
}
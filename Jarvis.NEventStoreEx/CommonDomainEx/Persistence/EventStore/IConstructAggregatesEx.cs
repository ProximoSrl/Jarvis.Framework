using System;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
    public interface IConstructAggregatesEx 
    {
        IAggregateEx Build(Type type, IIdentity id, IMementoEx snapshot);
    }
}
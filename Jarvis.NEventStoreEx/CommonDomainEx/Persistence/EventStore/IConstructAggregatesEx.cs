using NEventStore;
using System;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
    public interface IConstructAggregatesEx 
    {
        IAggregateEx Build(Type type, IIdentity id);

		Boolean ApplySnapshot(IAggregateEx aggregate, ISnapshot snapshot);
    }
}
using System;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public interface ISagaFactory
    {
        ISagaEx Build(Type sagaType, String id);
    }
}
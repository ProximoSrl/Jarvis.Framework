using System;
using System.Collections.Generic;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public interface ISagaRepositoryEx 
    {
        TSaga GetById<TSaga>(String sagaId) where TSaga : class, ISagaEx;

        void Save(ISagaEx saga, Guid commitId, Action<IDictionary<string, object>> updateHeaders);
    }
}
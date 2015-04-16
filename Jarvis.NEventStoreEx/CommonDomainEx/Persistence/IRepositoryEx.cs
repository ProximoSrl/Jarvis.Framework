using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public interface IRepositoryEx : IDisposable
    {
        TAggregate GetById<TAggregate>(IIdentity id) where TAggregate : class, IAggregateEx;

        TAggregate GetById<TAggregate>(IIdentity id, int version) where TAggregate : class, IAggregateEx;

        TAggregate GetById<TAggregate>(string bucketId, IIdentity id) where TAggregate : class, IAggregateEx;

        TAggregate GetById<TAggregate>(string bucketId, IIdentity id, int version) where TAggregate : class, IAggregateEx;

        void Save(IAggregateEx aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders);

        void Save(string bucketId, IAggregateEx aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders);
    }
}

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="aggregate"></param>
        /// <param name="commitId"></param>
        /// <param name="updateHeaders"></param>
        /// <returns>The number of events saved with this operation.</returns>
        Int32 Save(IAggregateEx aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bucketId"></param>
        /// <param name="aggregate"></param>
        /// <param name="commitId"></param>
        /// <param name="updateHeaders">The number of events saved with this operation.</param>
        /// <returns></returns>
        Int32 Save(string bucketId, IAggregateEx aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders);
    }

  
}

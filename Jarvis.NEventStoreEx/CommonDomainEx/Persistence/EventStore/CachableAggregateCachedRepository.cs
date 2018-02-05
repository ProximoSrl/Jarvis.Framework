using NStore.Aggregates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly

    /// <summary>
    /// This class implement the <see cref="IAggregateCachedRepository{TAggregate}"/>
    /// interface that can be stored in cache. It does not dispose wrapped repository
    /// and let outer code to manage lifecycle of the wrapped repository to be reused.
    /// </summary>
    /// <typeparam name="TAggregate"></typeparam>
    public class CachableAggregateCachedRepository<TAggregate> :
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
        IAggregateCachedRepository<TAggregate> where TAggregate : class, IAggregate
    {
        private readonly IRepository _wrappedRepository;
        private readonly Action<String, IRepository> _disposeAction;
        private readonly Action<String, IRepository, IAggregate> _afterSaveAction;
        private readonly Action<String, IRepository> _onExceptionAction;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="wrappedRepository"></param>
        /// <param name="aggregate">When aggregate is not in cache this parameter is null and the 
        /// instance of the aggregate is taken from the repository. </param>
        /// <param name="disposeAction"></param>
        /// <param name="afterSaveAction"></param>
        /// <param name="onExceptionAction"></param>
        /// <param name="id"></param>
        public CachableAggregateCachedRepository(
            IRepository wrappedRepository,
            TAggregate aggregate,
            Action<String, IRepository> disposeAction,
            Action<String, IRepository, IAggregate> afterSaveAction,
            Action<String, IRepository> onExceptionAction,
            IIdentity id)
        {
            _wrappedRepository = wrappedRepository;
            _disposeAction = disposeAction;
            _afterSaveAction = afterSaveAction;
            _onExceptionAction = onExceptionAction;
            Aggregate = aggregate ?? wrappedRepository.GetById<TAggregate>(id.ToString()).Result;
        }

        public TAggregate Aggregate { get; }

        /// <summary>
        /// Since the repository should be kept in cache, the dispose of this
        /// object only inform the caller with the callback that the single entity repository
        /// was disposed. 
        /// </summary>
        public void Dispose()
        {
            //do not dispose the wrapped repository, it should be reused, call the callback instead
            _disposeAction(Aggregate.Id, _wrappedRepository);
        }

        public void Save(Guid commitId, Action<IHeadersAccessor> updateHeaders)
        {
            try
            {
                _wrappedRepository.Save(Aggregate, commitId.ToString(), updateHeaders);
                _afterSaveAction(Aggregate.Id, _wrappedRepository, Aggregate);
            }
            catch (Exception)
            {
                _onExceptionAction(Aggregate.Id, _wrappedRepository);
                throw;
            }
        }
    }
}

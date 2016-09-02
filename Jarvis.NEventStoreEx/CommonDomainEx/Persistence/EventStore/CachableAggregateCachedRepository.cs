using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
    /// <summary>
    /// This class implement the <see cref="IAggregateCachedRepository{TAggregate}"/>
    /// interface that can be stored in cache. It does not dispose wrapped repository
    /// and let outer code to manage lifecycle of the wrapped repository to be reused.
    /// </summary>
    /// <typeparam name="TAggregate"></typeparam>
    public class CachableAggregateCachedRepository<TAggregate> : IAggregateCachedRepository<TAggregate> where TAggregate : class, IAggregateEx
    {
        readonly IRepositoryEx _wrappedRepository;
        readonly Action<IIdentity, IRepositoryEx> _disposeAction;
        readonly Action<IIdentity, IRepositoryEx, IAggregateEx> _afterSaveAction;
        readonly Action<IIdentity, IRepositoryEx> _onExceptionAction;
        readonly TAggregate _aggregate;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wrappedRepository"></param>
        /// <param name="aggregate">When aggregate is not in cache this parameter is null and the 
        /// instance of the aggregate is taken from the repository. </param>
        /// <param name="disposeAction"></param>
        /// <param name="afterSaveAction"></param>
        /// <param name="onExceptionAction"></param>
        /// <param name="id"></param>
        public CachableAggregateCachedRepository(
            IRepositoryEx wrappedRepository,
            TAggregate aggregate,
            Action<IIdentity, IRepositoryEx> disposeAction,
            Action<IIdentity, IRepositoryEx, IAggregateEx> afterSaveAction,
            Action<IIdentity, IRepositoryEx> onExceptionAction,
            IIdentity id)
        {
            _wrappedRepository = wrappedRepository;
            _disposeAction = disposeAction;
            _afterSaveAction = afterSaveAction;
            _onExceptionAction = onExceptionAction;
            _aggregate = aggregate ?? wrappedRepository.GetById<TAggregate>(id);
        }

        public TAggregate Aggregate
        {
            get
            {
                return _aggregate;
            }
        }

        /// <summary>
        /// Since the repository should be kept in cache, the dispose of this
        /// object only inform the caller with the callback that the single entity repository
        /// was disposed. 
        /// </summary>
        public void Dispose()
        {
            //do not dispose the wrapped repository, it should be reused, call the callback instead
            _disposeAction(_aggregate.Id, _wrappedRepository);
        }

        public void Save(Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            try
            {
                _wrappedRepository.Save(_aggregate, commitId, updateHeaders);
                _afterSaveAction(_aggregate.Id, _wrappedRepository, _aggregate);
            }
            catch (Exception)
            {
                _onExceptionAction(_aggregate.Id, _wrappedRepository);
                throw;
            }
        }
    }
}

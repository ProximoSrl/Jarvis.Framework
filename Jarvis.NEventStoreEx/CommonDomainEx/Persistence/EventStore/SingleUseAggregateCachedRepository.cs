using System;
using System.Collections.Generic;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
    /// <summary>
    /// This is a wrapper for <see cref="IAggregateCachedRepository{TAggregate}"/> that cannot be cached 
    /// and will be used for one shot only. It is necessary if a lock cannot be acquired or if the 
    /// user disabled the cache for repository.
    /// </summary>
    /// <typeparam name="TAggregate"></typeparam>
    public class SingleUseAggregateCachedRepository<TAggregate> : IAggregateCachedRepository<TAggregate> where TAggregate : class, IAggregateEx
    {
        readonly IRepositoryEx _wrappedRepository;
		readonly IRepositoryExFactory _repositoryFactory;
		readonly TAggregate _aggregate;

        public SingleUseAggregateCachedRepository(
            IRepositoryExFactory repositoryFactory,
            IIdentity id)
        {
			_repositoryFactory = repositoryFactory;
			_wrappedRepository = _repositoryFactory.Create();
            _aggregate = _wrappedRepository.GetById<TAggregate>(id);
        }

        public TAggregate Aggregate
        {
            get
            {
                return _aggregate;
            }
        }

        /// <summary>
        /// This is a single use repository, so it need to dispose wrapped resources because
        /// wrapped repository will not be reused.
        /// </summary>
        public void Dispose()
        {
			_repositoryFactory.Release(_wrappedRepository);
        }

        /// <summary>
        /// Simply save the entity with wrapped repository.
        /// </summary>
        /// <param name="commitId"></param>
        /// <param name="updateHeaders"></param>
        public void Save(Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            _wrappedRepository.Save(_aggregate, commitId, updateHeaders);
        }
    }
}
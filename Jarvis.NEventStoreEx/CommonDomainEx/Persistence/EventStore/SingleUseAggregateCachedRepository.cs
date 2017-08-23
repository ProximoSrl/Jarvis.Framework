using NStore.Aggregates;
using System;
using System.Collections.Generic;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly
    /// <summary>
    /// This is a wrapper for <see cref="IAggregateCachedRepository{TAggregate}"/> that cannot be cached 
    /// and will be used for one shot only. It is necessary if a lock cannot be acquired or if the 
    /// user disabled the cache for repository.
    /// </summary>
    /// <typeparam name="TAggregate"></typeparam>
    public class SingleUseAggregateCachedRepository<TAggregate> : IAggregateCachedRepository<TAggregate> where TAggregate : class, IAggregate
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
    {
        readonly IRepository _wrappedRepository;
		readonly IRepositoryFactory _repositoryFactory;

        public SingleUseAggregateCachedRepository(
            IRepositoryFactory repositoryFactory,
            IIdentity id)
        {
			_repositoryFactory = repositoryFactory;
			_wrappedRepository = _repositoryFactory.Create();
            Aggregate = _wrappedRepository.GetById<TAggregate>(id.AsString()).Result;
        }

        public TAggregate Aggregate { get; }

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
        public void Save(Guid commitId, Action<IHeadersAccessor> updateHeaders)
        {
            _wrappedRepository.Save(Aggregate, commitId.ToString(), updateHeaders);
        }
    }
}
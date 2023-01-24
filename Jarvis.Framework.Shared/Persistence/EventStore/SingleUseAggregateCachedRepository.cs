using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Domain;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Persistence.EventStore
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly
	/// <summary>
	/// This is a wrapper for <see cref="IAggregateCachedRepository{TAggregate}"/> that cannot be cached
	/// and will be used for one shot only. It is necessary if a lock cannot be acquired or if the
	/// user disabled the cache for repository. When you use this repository NO AGGREGATE cache is used.
	/// </summary>
	/// <typeparam name="TAggregate"></typeparam>
	public class SingleUseAggregateCachedRepository<TAggregate> : IAggregateCachedRepository<TAggregate> where TAggregate : class, IAggregate
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
	{
		private readonly IRepository _wrappedRepository;
		private readonly IRepositoryFactory _repositoryFactory;

		public SingleUseAggregateCachedRepository(
			IRepositoryFactory repositoryFactory,
			IIdentity id)
		{
			_repositoryFactory = repositoryFactory;
			_wrappedRepository = _repositoryFactory.Create();
			Aggregate = _wrappedRepository.GetByIdAsync<TAggregate>(id.AsString()).Result;
		}

		/// <inheritdoc />
		public TAggregate Aggregate { get; private set; }

		/// <summary>
		/// This is a single use repository, so it need to dispose wrapped resources because
		/// wrapped repository will not be reused.
		/// </summary>
		public void Dispose()
		{
			Clear();
			_repositoryFactory.Release(_wrappedRepository);
		}

		/// <summary>
		/// Simply save the entity with wrapped repository.
		/// </summary>
		/// <param name="commitId"></param>
		/// <param name="updateHeaders"></param>
		public Task SaveAsync(Guid commitId, Action<IHeadersAccessor> updateHeaders)
		{
			if (Aggregate == null)
			{
				throw new JarvisFrameworkEngineException("Cannot Save Aggregate in repository because the repository was cleared. Cause of the error is reusing repository after exception."); ;
			}
			try
			{
				return _wrappedRepository.SaveAsync(Aggregate, commitId.ToString(), updateHeaders);
			}
			catch (Exception)
			{
				//Aggregate cannot be reused after an exception, we need to clear aggregate. This prevent erratic reuse of cached aggregate.
				Clear();
				//Simply rethrow the exception.
				throw;
			}
		}

		private void Clear()
		{
			Aggregate = null;
		}
	}
}
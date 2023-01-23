using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Domain;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Persistence
{
	/// <summary>
	/// This is a really reduced repository that wraps access to a single entity, it is useful
	/// because it can be cached for a single aggregate.
	/// </summary>
	/// <typeparam name="TAggregate"></typeparam>
	public interface IAggregateCachedRepository<TAggregate> : IDisposable where TAggregate : class, IAggregate
	{
		/// <summary>
		/// This is in-memory aggregate that is used to re-use aggretate and reduce number of load
		/// from snapshot
		/// </summary>
		TAggregate Aggregate { get; }

		/// <summary>
		/// Save the aggregate to the underling stream.
		/// </summary>
		/// <param name="commitId"></param>
		/// <param name="updateHeaders"></param>
		/// <returns></returns>
		Task SaveAsync(Guid commitId, Action<IHeadersAccessor> updateHeaders);
	}

	/// <summary>
	/// Abstract the factory for a repository with single entity, needed to use cache.
	/// </summary>
	public interface IAggregateCachedRepositoryFactory
	{
		/// <summary>
		/// <para>
		/// It is IMPORTANT to notice that the default implementation of this method
		/// can return a single aggregate repository with an entity in cache, so it is
		/// IMPORTANT that you prevent multiple thread to ask and use for the very same
		/// aggregateId.
		/// </para>
		/// <para>
		/// It is imperative that this call is single thread for each <paramref name="id"/> passed as argument
		/// or you will access the SAME aggregate from two different thread and this is a recipie for disaster.
		/// </para>
		/// </summary>
		/// <typeparam name="TAggregate"></typeparam>
		/// <param name="id">The id of the aggregate, remember to Lock to prevent
		/// multiple threads to use the same aggregate concurrently.</param>
		/// <returns></returns>
		IAggregateCachedRepository<TAggregate> Create<TAggregate>(EventStoreIdentity id) where TAggregate : class, IAggregate;

		/// <summary>
		/// Release repository after use, this will put the repository back into the pool of cached repositories.
		/// </summary>
		/// <typeparam name="TAggregate"></typeparam>
		/// <param name="repository"></param>
		void Release<TAggregate>(IAggregateCachedRepository<TAggregate> repository) where TAggregate : class, IAggregate;
	}
}

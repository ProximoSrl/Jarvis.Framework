using Jarvis.Framework.Shared.ReadModel.Atomic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    /// <summary>
    /// Simple interface to expose <see cref="IQueryable{T}"/> interface,
    /// so we can take advange of covariance in readmodels too.
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    public interface IAtomicCollectionReaderQueryable<out TModel>
        where TModel : IAtomicReadModel
    {
        /// <summary>
        /// Allow standard IQueryable interface
        /// </summary>
        IQueryable<TModel> AsQueryable();

        /// <summary>
        /// <para>
        /// This is the same of <see cref="AsQueryable"/>, but this will read preferred on secondary if we have replicaset.
        /// Actually we have all readmodels on mongodb, so this is somewhat leaked abstraction. If we will ever use a SQL
        /// Server or other storage, this method will be probably is equal to <see cref="AsQueryable"/>.
        /// </para>
        /// <para>
        /// This is used to allow reading on secondary in mongo, accepting the fact that we can read stale data but not 
        /// using the primary.
        /// </para>
        /// </summary>
        IQueryable<TModel> AsQueryableSecondaryPreferred();
    }

    /// <summary>
    /// Simple wrapper for readmodel atomic collection. There is no need
    /// for corresponding reader, this is the only interface that will
    /// be used to access atomic readmodels, that are inherited by <see cref="AbstractAtomicReadModel"/>
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    public interface IAtomicCollectionReader<TModel>
        : IAtomicCollectionReaderQueryable<TModel>
        where TModel : IAtomicReadModel
    {
        /// <summary>
        /// Find a readmodel by id and does some checking like automatic rebuild
        /// of superseded readmodel, etc.
        /// </summary>
        /// <param name="id">The id of the readmodel to find</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The readmodel instance or null if not found</returns>
        Task<TModel> FindOneByIdAsync(String id, CancellationToken cancellationToken = default);

        /// <summary>
        /// This is similar to <see cref="FindOneByIdAsync(string)"/> but it will re-query event stream
        /// to project extra events. This ensure that you got the must up-to-date readmodel.
        /// </summary>
        /// <param name="id">The id of the readmodel to find</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The readmodel instance with latest events projected or null if not found</returns>
        Task<TModel> FindOneByIdAndCatchupAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Find by query and allow for an optional parameter called <paramref name="fixVersion"/> that allows
        /// to fix for version if you need to be 100% sure to read latest version (upgrade skew)
        /// </summary>
        /// <param name="filter">Filter expression to match readmodels</param>
        /// <param name="fixVersion">If true, ensures all returned readmodels are at the latest version</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A collection of matching readmodels</returns>
        Task<IReadOnlyCollection<TModel>> FindManyAsync(System.Linq.Expressions.Expression<Func<TModel, bool>> filter, bool fixVersion = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads by id but project until a specific checkpoint passed as parameter. This checkpoint
        /// is the global checkpoint of the store (not Aggregate Version).
        /// </summary>
        /// <param name="id">aggregate id</param>
        /// <param name="chunkPosition">
        /// Position to project the aggregate to.
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The readmodel projected up to the specified checkpoint or null if not found</returns>
        /// <remarks>Actually this method is a simple wrapper to a call to <see cref="ILiveAtomicReadModelProcessor"/>
        /// that was used internally by the reader.</remarks>
        Task<TModel> FindOneByIdAtCheckpointAsync(string id, long chunkPosition, CancellationToken cancellationToken = default);

        /// <summary>
        /// <para>
        /// Find multiple readmodels by filter and perform batch catchup to ensure they are all
        /// up-to-date with the latest events from their respective aggregate streams.
        /// </para>
        /// <para>
        /// This method uses <see cref="ILiveAtomicReadModelProcessor.CatchupBatchAsync{TModel}"/>
        /// internally to efficiently read events for multiple aggregates in a single batch operation.
        /// </para>
        /// </summary>
        /// <param name="filter">Filter expression to match readmodels</param>
        /// <param name="maxBatchSize">Maximum number of readmodels to process in a single batch.
        /// Larger batches are more efficient but use more memory. Defaults to 100.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of readmodels with latest events projected. The readmodels are
        /// modified in-place and returned.</returns>
        /// <remarks>
        /// <para>
        /// This method:
        /// 1. Loads readmodels matching the filter from storage
        /// 2. For each readmodel, reads events from NStore starting from AggregateVersion + 1
        /// 3. Returns all readmodels with the latest events applied in-memory
        /// </para>
        /// <para>
        /// Note: This does NOT persist the updated readmodels back to storage. The caller is
        /// responsible for persisting if needed.
        /// </para>
        /// </remarks>
        Task<IReadOnlyCollection<TModel>> FindManyAndCatchupAsync(
            System.Linq.Expressions.Expression<Func<TModel, bool>> filter,
            int maxBatchSize = 100,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// <para>
        /// Find or create readmodels for a list of aggregate IDs and perform batch catchup to ensure
        /// they are all up-to-date with the latest events from their respective aggregate streams.
        /// </para>
        /// <para>
        /// Unlike <see cref="FindManyAndCatchupAsync"/>, this method will create new readmodels
        /// for IDs that don't exist in storage but have events in NStore. This is useful when you
        /// have a list of aggregate IDs and want to ensure all readmodels are projected, even if
        /// they haven't been persisted yet.
        /// </para>
        /// </summary>
        /// <param name="idList">List of aggregate IDs to find or create readmodels for</param>
        /// <param name="maxBatchSize">Maximum number of readmodels to process in a single batch.
        /// Larger batches are more efficient but use more memory. Defaults to 100.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of readmodels with latest events projected. Returns only readmodels
        /// that have at least one event (AggregateVersion > 0). IDs with no events are excluded.</returns>
        /// <remarks>
        /// <para>
        /// This method:
        /// 1. Loads existing readmodels from storage for the given IDs
        /// 2. Creates new readmodel instances for IDs not found in storage
        /// 3. Performs batch catchup for all readmodels (existing and new)
        /// 4. Returns only readmodels with AggregateVersion > 0 (excludes IDs with no events)
        /// </para>
        /// <para>
        /// Note: This does NOT persist the updated or newly created readmodels back to storage.
        /// The caller is responsible for persisting if needed.
        /// </para>
        /// </remarks>
        Task<IReadOnlyCollection<TModel>> FindManyByIdListAndCatchupAsync(
            IEnumerable<string> idList,
            int maxBatchSize = 100,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Needed for the fixer, a factory NON GENERIC that have a generic method to 
    /// create CollectionWrapper
    /// </summary>
    public interface IAtomicCollectionWrapperFactory
    {
        IAtomicCollectionWrapper<TModel> CreateCollectionWrappper<TModel>()
             where TModel : IAtomicReadModel;
    }

    /// <summary>
    /// General interface to access storage for atomic readmodel.
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    public interface IAtomicCollectionWrapper<TModel> :
        IAtomicCollectionReader<TModel>
        where TModel : IAtomicReadModel
    {
        /// <summary>
        /// Insert or update a readmodel instance, avoid any AggregateVersion check, it will
        /// overwrite the value on the database even if the AggregateVersion of <paramref name="model"/> is
        /// lower than the version on disk
        /// </summary>
        /// <param name="model">The readmodel instance to upsert</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>Readmodel global signature check is always performed.</remarks>
        Task UpsertForceAsync(TModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// Insert or update a readmodel instance, perform check for idempotency and
        /// readmodel versioning.
        /// </summary>
        /// <param name="model">The readmodel instance to upsert</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task UpsertAsync(TModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// <para>
        /// Insert or update multiple readmodel instances in a single batch operation.
        /// This method leverages MongoDB's BulkWrite API for efficient batch processing.
        /// </para>
        /// <para>
        /// For each model in the batch, the method performs idempotency checks based on
        /// AggregateVersion and ReadModelVersion. Models with ModifiedWithExtraStreamEvents
        /// flag are automatically skipped. If a model already exists with a higher version,
        /// it will not be overwritten.
        /// </para>
        /// <para>
        /// The operation attempts to use a ReplaceOne with upsert for each model. When
        /// a model doesn't exist on disk, it will be inserted; when it exists, it will
        /// be replaced only if the new version is higher.
        /// </para>
        /// </summary>
        /// <param name="models">Collection of readmodel instances to upsert. Must not contain duplicate Ids.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous batch operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when models collection is null</exception>
        /// <exception cref="CollectionWrapperException">Thrown when any model has an empty Id or when duplicate Ids are present in the batch</exception>
        /// <exception cref="MongoException">Thrown when MongoDB operations fail</exception>
        /// <remarks>
        /// This method is significantly more efficient than calling UpsertAsync multiple times
        /// when dealing with large batches of readmodels. Empty collections are handled gracefully.
        /// The batch must not contain duplicate readmodel Ids.
        /// </remarks>
        Task UpsertBatchAsync(IEnumerable<TModel> models, CancellationToken cancellationToken = default);

        /// <summary>
        /// <para>
        /// Update a readmodel instance into underling storage, it perform
        /// it will check for idempotency, if the object was already saved
        /// and it is newer it will not save again. 
        /// </para>
        /// </summary>
        /// <param name="model">The readmodel instance to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>If the readmodel is not present on database, nothing will be written.</remarks>
        Task UpdateAsync(TModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// <para>
        /// Used in the following scenario.
        /// I have a readmodel in version X = 6
        /// Changeset 7 and 8 are applied but the readmodel does not handle events in those changeset
        /// Usually projection service will not update the readmodel on Storage because it is not changed by the
        /// events in the changeset.
        /// Problem: Aggregate on disk is on AggregateVersion=6 and events 7 and 8 are not in the list of handled commits.
        /// This generates lots of problem because it made almost impossible to diagnose errors.
        /// </para>
        /// <para>
        /// Possible solution: Alwasy save the readmodel even if it declare that events does not change it.
        /// Best solution: Call this method that will update only the relevant properties of the changes that will indicates
        /// the real list of <see cref="Changeset"/> applied.
        /// </para>
        /// </summary>
        /// <param name="model">The readmodel instance with updated version information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task UpdateVersionAsync(TModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// <para>
        /// Batch version of <see cref="UpdateVersionAsync"/>. Updates only version-related properties
        /// (ProjectedPosition, AggregateVersion, LastProcessedVersions) for multiple readmodel instances
        /// in a single efficient operation using MongoDB's BulkWrite API.
        /// </para>
        /// <para>
        /// This method is designed for scenarios where readmodels were not modified by events in a changeset
        /// but still need their version tracking updated to reflect processed stream positions. It performs
        /// partial updates instead of full document replacement, making it safer and more efficient than
        /// using UpsertBatchAsync for unmodified readmodels.
        /// </para>
        /// <para>
        /// For each model in the batch:
        /// - If the readmodel exists on disk, performs a partial update of version fields only
        /// - If the readmodel doesn't exist, inserts the full model (same fallback behavior as UpdateVersionAsync)
        /// - Models with ModifiedWithExtraStreamEvents flag are automatically skipped
        /// </para>
        /// </summary>
        /// <param name="models">Collection of readmodel instances with updated version information. Must not contain duplicate Ids.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous batch operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when models collection is null</exception>
        /// <exception cref="CollectionWrapperException">Thrown when any model has an empty Id or when duplicate Ids are present in the batch</exception>
        /// <exception cref="MongoException">Thrown when MongoDB operations fail</exception>
        /// <remarks>
        /// This method is significantly more efficient than calling UpdateVersionAsync multiple times
        /// and is safer than UpsertBatchAsync for readmodels that haven't changed, as it only touches
        /// version-tracking fields. Empty collections are handled gracefully.
        /// The batch must not contain duplicate readmodel Ids.
        /// </remarks>
        Task UpdateVersionBatchAsync(IEnumerable<TModel> models, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a readmodel instance from the underlying storage by its id.
        /// </summary>
        /// <param name="id">The id of the readmodel to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
    /// <summary>
    /// Processor that is capable of live projection of atomic
    /// readmodels, it will allow for historical projection when
    /// you have a readmodel and you want to project up to a certain
    /// point in time.
    /// </summary>
    public interface ILiveAtomicReadModelProcessor
    {
        /// <summary>
        /// Project a readmodel until a certain version of the aggregate.
        /// </summary>
        /// <typeparam name="TModel">Type of atomic readmodel to be projected</typeparam>
        /// <param name="id">If of the aggregate</param>
        /// <param name="versionUpTo">Version of aggregate to project, it is NOT the global
        /// position, but version of aggregate.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TModel> ProcessAsync<TModel>(
            String id,
            Int64 versionUpTo,
            CancellationToken cancellationToken = default)
            where TModel : IAtomicReadModel;

        /// <summary>
        /// For temporal reamodel we can be interested in projecting a readmodel
        /// until a certain position relative to the entire stream.
        /// </summary>
        /// <typeparam name="TModel">Type of atomic readmodel to be projected</typeparam>
        /// <param name="id">If of the aggregate</param>
        /// <param name="positionUpTo">Global position in the stream to use for projection.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TModel> ProcessUntilChunkPositionAsync<TModel>(
            String id,
            Int64 positionUpTo,
            CancellationToken cancellationToken = default)
            where TModel : IAtomicReadModel;

        /// <summary>
        /// For temporal reamodel we can be interested in projecting a readmodel up to a certain
        /// date in time. We know that date can be tricky, but it is a convenient method to be
        /// used because we do not have timestamp as first level citizen in NStore.
        /// </summary>
        /// <typeparam name="TModel">Type of atomic readmodel to be projected</typeparam>
        /// <param name="id">If of the aggregate</param>
        /// <param name="dateTimeUpTo">Date to project up to, you must specify UTC date time value not local time</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TModel> ProcessUntilUtcTimestampAsync<TModel>(
            String id,
            DateTime dateTimeUpTo,
            CancellationToken cancellationToken = default)
            where TModel : IAtomicReadModel;

        /// <summary>
        /// If you have a readmodel in memory but you are not sure if it is the result of most
        /// up-to-date projection (maybe projection service is slow or stopped) you can call
        /// this method to query NStore and reapply newer events if present. At the end of the call
        /// <paramref name="readModel"/> is up-to-date with the latest commit in aggregate stream
        /// </summary>
        /// <param name="readModel">Readmodel to catchup, if there are newer events not still projected
        /// at the end of this method this reamodel will be modified applying missing events.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CatchupAsync<TModel>(
            TModel readModel,
            CancellationToken cancellationToken = default)
            where TModel : IAtomicReadModel;

        /// <summary>
        /// <para>
        /// Batch catchup for multiple readmodels of the same type. Each readmodel may be at a different
        /// aggregate version, and this method will efficiently read all required events from NStore
        /// using <c>ReadForwardMultiplePartitionsWithRangesAsync</c>, which allows reading multiple
        /// partitions each starting from a different position.
        /// </para>
        /// <para>
        /// At the end of the call, all readmodels in the collection are up-to-date with the latest
        /// commits in their respective aggregate streams. The readmodels are modified in-place.
        /// </para>
        /// </summary>
        /// <typeparam name="TModel">Type of atomic readmodel to catchup</typeparam>
        /// <param name="readModels">Collection of readmodels to catchup. Each readmodel will have
        /// events read starting from its <c>AggregateVersion + 1</c>. The collection must not contain
        /// duplicate aggregate ids.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when all readmodels have been caught up</returns>
        /// <exception cref="ArgumentNullException">Thrown when readModels is null</exception>
        Task CatchupBatchAsync<TModel>(
            IReadOnlyCollection<TModel> readModels,
            CancellationToken cancellationToken = default)
            where TModel : IAtomicReadModel;
    }
}

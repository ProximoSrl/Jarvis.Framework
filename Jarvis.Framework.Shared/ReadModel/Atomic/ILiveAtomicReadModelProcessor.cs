using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
    /// <summary>
    /// Processor that is capable of live projection of atomic
    /// readmodel
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
        /// <returns></returns>
        Task<TModel> ProcessAsync<TModel>(String id, Int64 versionUpTo)
            where TModel : IAtomicReadModel;

        /// <summary>
        /// For temporal reamodel we can be interested in projecting a readmodel
        /// until a certain position relative to the entire stream.
        /// </summary>
        /// <typeparam name="TModel">Type of atomic readmodel to be projected</typeparam>
        /// <param name="id">If of the aggregate</param>
        /// <param name="positionUpTo">Global position in the stream to use for projection.</param>
        /// <returns></returns>
        Task<TModel> ProcessAsyncUntilChunkPosition<TModel>(String id, Int64 positionUpTo)
            where TModel : IAtomicReadModel;

        /// <summary>
        /// For temporal reamodel we can be interested in projecting a readmodel up to a certain
        /// date in time. We know that date can be tricky, but it is a convenient method to be
        /// used because we do not have timestamp as first level citizen in NStore.
        /// </summary>
        /// <typeparam name="TModel">Type of atomic readmodel to be projected</typeparam>
        /// <param name="id">If of the aggregate</param>
        /// <param name="dateTimeUpTo">Date to project up to, you must specify UTC date time value not local time</param>
        /// <returns></returns>
        Task<TModel> ProcessAsyncUntilUtcTimestamp<TModel>(String id, DateTime dateTimeUpTo)
            where TModel : IAtomicReadModel;

        /// <summary>
        /// If you have a readmodel in memory but you are not sure if it is the result of most
        /// up-to-date projection (maybe projection service is slow or stopped) you can call
        /// this method to query NStore and reapply newer events if present. At the end of the call
        /// <paramref name="readModel"/> is up-to-date with the latest commit in aggregate stream
        /// </summary>
        /// <param name="readModel">Readmodel to catchup, if there are newer events not still projected
        /// at the end of this method this reamodel will be modified applying missing events.</param>
        /// <returns></returns>
        Task CatchupAsync<TModel>(TModel readModel)
            where TModel : IAtomicReadModel;
    }
}

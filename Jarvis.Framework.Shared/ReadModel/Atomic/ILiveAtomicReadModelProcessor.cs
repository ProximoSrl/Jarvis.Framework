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
        /// <typeparam name="TModel"></typeparam>
        /// <param name="id"></param>
        /// <param name="versionUpTo"></param>
        /// <returns></returns>
        Task<TModel> ProcessAsync<TModel>(String id, Int64 versionUpTo)
            where TModel : IAtomicReadModel;

        /// <summary>
        /// For temporal reamodel we can be interested in projecting a readmodel
        /// until a certain position relative to the entire stream.
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="id"></param>
        /// <param name="positionUpTo"></param>
        /// <returns></returns>
        Task<TModel> ProcessAsyncUntilChunkPosition<TModel>(String id, Int64 positionUpTo)
            where TModel : IAtomicReadModel;
    }
}

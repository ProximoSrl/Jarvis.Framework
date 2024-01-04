using Jarvis.Framework.Shared.ReadModel.Atomic;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    /// <summary>
    /// <para>
    /// <see cref="LiveAtomicReadModelProcessor"/> is used to rebuild atomic readmodels
    /// at specific checkpoints, to speedup the process we can have a cache component
    /// that is able to cache readmodel at different checkpoints.
    /// </para>
    /// <para>
    /// It is imperative that the cache does not store the readmodel in memory, it must
    /// persist in a storage or clone so the original instance is not used because it will
    /// modified by the processor.
    /// </para>
    /// </summary>
    public interface ILiveAtomicreadmodelProcessorCache
    {
        /// <summary>
        /// This method is called to have a readmodel of type T at a specific checkpoint or at
        /// a version lesser than <paramref name="versionUpTo"/>. The purpose is being able not
        /// to start from the beginning of the stream, but to start from a specific checkpoint with
        /// a readmodel that is already partially built.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <param name="versionUpTo"></param>
        /// <returns>A readmodel saved in cache, with the same id, of type <typeparamref name="T"/> and
        /// version up to the specified aggregate version value</returns>
        Task<T> GetReadmodelAtVersionAsync<T>(String id, Int64 versionUpTo) where T : IAtomicReadModel;

        /// <summary>
        /// Completely equal to <see cref="GetReadmodelAtVersionAsync{T}(string, long)"/> but this version
        /// uses the GLOBAL position at checkpoint instead of the aggregate version.
        /// This method is called to have a readmodel of type T at a specific checkpoint or at
        /// a position lesser than <paramref name="positionUpTo"/>. The purpose is being able not
        /// to start from the beginning of the stream, but to start from a specific checkpoint with
        /// a readmodel that is already partially built.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <param name="positionUpTo"></param>
        /// <returns>A readmodel saved in cache, with the same id, of type <typeparamref name="T"/> and
        /// version up to the specified aggregate version value</returns>
        Task<T> GetReadmodelAtCheckpointAsync<T>(String id, Int64 positionUpTo) where T : IAtomicReadModel;

        IReadOnlyCollection<IAtomicReadModel> GetCacheForMultipleReadmodel(
            IReadOnlyCollection<MultiStreamProcessRequest> request,
            Int64 checkpointUpToIncluded);

        /// <summary>
        /// Persist a readmodel in cache, this method is called by the processor when it has finished
        /// rebuilding a readmodel.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="readmodel"></param>
        /// <returns></returns>
        Task SaveReadmodelInCacheAsync<T>(T readmodel) where T : IAtomicReadModel;
    }

    /// <summary>
    /// A null object implementation to avoid null checks.
    /// </summary>
    public class NullLiveAtomicreadmodelProcessorCache : ILiveAtomicreadmodelProcessorCache
    {
        public IReadOnlyCollection<IAtomicReadModel> GetCacheForMultipleReadmodel(IReadOnlyCollection<MultiStreamProcessRequest> request, long checkpointUpToIncluded)
        {
            return Task.FromResult<T>(default);
        }

        /// <inheritdoc />
        public Task<T> GetReadmodelAtCheckpointAsync<T>(string _, long __) where T : IAtomicReadModel
        {
            return Task.FromResult<T>(default);
        }

        /// <inheritdoc />
        public Task<T> GetReadmodelAtVersionAsync<T>(string id, long versionUpTo) where T : IAtomicReadModel
        {
            return Task.FromResult<T>(default);
        }

        public Task SaveReadmodelInCacheAsync<T>(T readmodel) where T : IAtomicReadModel
        {
            return Task.CompletedTask;
        }
    }
}

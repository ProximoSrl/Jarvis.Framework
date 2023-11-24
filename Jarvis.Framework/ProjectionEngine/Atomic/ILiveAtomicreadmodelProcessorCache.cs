using Jarvis.Framework.Shared.ReadModel.Atomic;
using System;
using System.Collections.Generic;
using System.Text;

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
        /// <returns></returns>
        T GetReadmodelAtCheckpoint<T>(String id, Int64 versionUpTo) where T : IAtomicReadModel;
    }

    /// <summary>
    /// A null object implementation to avoid null checks.
    /// </summary>
    public class NullLiveAtomicreadmodelProcessorCache : ILiveAtomicreadmodelProcessorCache
    {
        public T GetReadmodelAtCheckpoint<T>(string id, long versionUpTo) where T : IAtomicReadModel
        {
            return default;
        }
    }
}

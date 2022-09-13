using NStore.Domain;
using System;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support
{
    /// <summary>
    /// Factory for consumers that will project <see cref="Changeset"/> into
    /// atomic projection engine. This interface is not generic and allow the creation
    /// of a consumer given a <see cref="Type"/> (non generic).
    /// </summary>
    public interface IAtomicReadmodelProjectorHelperFactory
    {
        /// <summary>
        /// Create consumer for a specific type of readmodel.
        /// </summary>
        /// <param name="atomicReadmodelType">Type of Atomic readmodel that we want to project</param>
        /// <returns></returns>
        IAtomicReadmodelProjectorHelper CreateFor(Type atomicReadmodelType);
    }
}

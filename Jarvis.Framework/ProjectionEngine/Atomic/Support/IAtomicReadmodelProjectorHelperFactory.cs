using System;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support
{
    /// <summary>
    /// Factory for the consumers.
    /// </summary>
    public interface IAtomicReadmodelProjectorHelperFactory
    {
        IAtomicReadmodelProjectorHelper CreateFor(Type atomicReadmodelType);
    }
}

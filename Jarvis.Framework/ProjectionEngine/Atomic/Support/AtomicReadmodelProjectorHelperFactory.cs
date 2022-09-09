using Castle.MicroKernel;
using System;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support
{
    /// <summary>
    /// This is useful because all <see cref="IAtomicCollectionWrapper{TModel}"/> classes are typed in T while
    /// the projection service works with type. This class will helps creating with reflection an <see cref="AtomicReadmodelProjectorHelper{TModel}"/>
    /// to consume changeset during projection.
    /// </summary>
    public class AtomicReadmodelProjectorHelperFactory : IAtomicReadmodelProjectorHelperFactory
    {
        private readonly IKernel _kernel;

        public AtomicReadmodelProjectorHelperFactory(IKernel kernel)
        {
            _kernel = kernel;
        }

        public IAtomicReadmodelProjectorHelper CreateFor(Type atomicReadmodelType)
        {
            var genericType = typeof(AtomicReadmodelProjectorHelper<>);
            var closedType = genericType.MakeGenericType(new Type[] { atomicReadmodelType });
            try
            {

                return (IAtomicReadmodelProjectorHelper)_kernel.Resolve(closedType);
            }
            catch (Exception ex)
            {

                throw;
            }
        }
    }
}

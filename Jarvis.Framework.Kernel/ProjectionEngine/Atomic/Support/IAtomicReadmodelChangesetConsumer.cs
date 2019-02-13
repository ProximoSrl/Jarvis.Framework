using Castle.MicroKernel;
using Castle.Windsor;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using NStore.Domain;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support
{
    /// <summary>
    /// Helper class to consume Changeset projected by projection engin.
    /// </summary>
    public interface IAtomicReadmodelChangesetConsumer
    {
        /// <summary>
        /// Simple consume a changeset applying it to the correct readmodel.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="changeset"></param>
        /// <param name="identity"></param>
        /// <returns>The instance of the AtomicReadmodelChangesetConsumerReturnValue after modification or null if the
        /// readmodel was not modified.</returns>
        Task<AtomicReadmodelChangesetConsumerReturnValue> Handle(
            Int64 position,
            Changeset changeset,
            IIdentity identity);

        /// <summary>
        /// A reference to the attribute of the readmodel that is handled by this instance
        /// </summary>
        AtomicReadmodelInfoAttribute AtomicReadmodelInfoAttribute { get; }
    }

    /// <summary>
    /// Return the result of handling a changeset.
    /// </summary>
    public class AtomicReadmodelChangesetConsumerReturnValue
    {
        public AtomicReadmodelChangesetConsumerReturnValue(IAtomicReadModel readmodel, bool createdForFirstTime)
        {
            Readmodel = readmodel;
            CreatedForFirstTime = createdForFirstTime;
        }

        public IAtomicReadModel Readmodel { get; set; }

        /// <summary>
        /// True if the readmodel was created for the first time
        /// </summary>
        public Boolean CreatedForFirstTime { get; set; }
    }

    /// <summary>
    /// Factory for the consumers.
    /// </summary>
    public interface IAtomicReadmodelChangesetConsumerFactory
    {
        IAtomicReadmodelChangesetConsumer CreateFor(Type atomicReadmodelType);
    }

    /// <summary>
    /// This is useful because all <see cref="IAtomicCollectionWrapper{TModel}"/> classes are typed in T while
    /// the projection service works with type. This class will helps creating with reflection an <see cref="AtomicReadmodelChangesetConsumer{TModel}"/>
    /// to consume changeset during projection.
    /// </summary>
    public class AtomicReadmodelChangesetConsumerFactory : IAtomicReadmodelChangesetConsumerFactory
    {
        private readonly IKernel _kernel;

        public AtomicReadmodelChangesetConsumerFactory(IKernel kernel)
        {
            _kernel = kernel;
        }

        public IAtomicReadmodelChangesetConsumer CreateFor(Type atomicReadmodelType)
        {
            var genericType = typeof(AtomicReadmodelChangesetConsumer<>);
            var closedType = genericType.MakeGenericType(new Type[] { atomicReadmodelType });
            return (IAtomicReadmodelChangesetConsumer)_kernel.Resolve(closedType);
        }
    }
}

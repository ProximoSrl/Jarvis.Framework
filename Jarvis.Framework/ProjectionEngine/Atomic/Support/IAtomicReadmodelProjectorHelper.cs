using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using NStore.Domain;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support
{
    /// <summary>
    /// Helper class to consume Changeset projected by projection engine, it is needed 
    /// to being able to project / handle readmodel without handling generics.
    /// </summary>
    public interface IAtomicReadmodelProjectorHelper
    {
        /// <summary>
        /// Simple consume a changeset applying it to the correct readmodel performing load, modify, save
        /// with the support of collection wrapper.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="changeset"></param>
        /// <param name="identity"></param>
        /// <returns>The instance of the AtomicReadmodelChangesetConsumerReturnValue after modification or null if the
        /// readmodel was not modified.</returns>
        Task<AtomicReadmodelChangesetConsumerReturnValue> Handle(
            Int64 position,
            Changeset changeset,
            IIdentity identity,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// This method will do a full projection reprojecting the entire stream into the database
        /// and it is useful for the catchup of new readmodel, because it is much faster to rebuild
        /// each readmodel with a simple roundtrip
        /// </summary>
        /// <param name="identity"></param>
        Task FullProjectAsync(IIdentity identity, CancellationToken cancellationToken = default);

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
}

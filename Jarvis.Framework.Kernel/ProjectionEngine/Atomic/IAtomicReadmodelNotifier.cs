using Jarvis.Framework.Shared.ReadModel.Atomic;
using NStore.Domain;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    /// <summary>
    /// Gives a way to user of framework the ability to be notified when an
    /// <see cref="IAtomicReadModel"/> is changed due a processing of a <see cref="Changeset"/>
    /// </summary>
    public interface IAtomicReadmodelNotifier
    {
        /// <summary>
        /// This method will be called when the readmodel will be creae
        /// </summary>
        /// <param name="atomicReadModel"></param>
        /// <param name="changeset"></param>
        /// <returns></returns>
        Task ReadmodelCreatedAsync(IAtomicReadModel atomicReadModel, Changeset changeset);

        /// <summary>
        /// This method will be called by projection service after an event is projected.
        /// </summary>
        /// <param name="atomicReadModel"></param>
        /// <param name="changeset"></param>
        Task ReadmodelUpdatedAsync(IAtomicReadModel atomicReadModel, Changeset changeset);
    }

    /// <summary>
    /// Atomic readmodel notifier to ignore everything.
    /// </summary>
    public class NullAtomicReadmodelNotifier : IAtomicReadmodelNotifier
    {
        public Task ReadmodelCreatedAsync(IAtomicReadModel atomicReadModel, Changeset changeset)
        {
            return Task.CompletedTask;
        }

        public Task ReadmodelUpdatedAsync(IAtomicReadModel atomicReadModel, Changeset changeset)
        {
            return Task.CompletedTask;
        }
    }
}

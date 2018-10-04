using Castle.Core.Logging;
using Castle.Windsor;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using NStore.Domain;
using System;
using System.Linq;
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
        /// <returns></returns>
        Task Handle(
            Int64 position,
            Changeset changeset,
            IIdentity identity);

        /// <summary>
        /// A reference to the attribute of the readmodel that is handled by this instance
        /// </summary>
        AtomicReadmodelInfoAttribute AtomicReadmodelInfoAttribute { get; }
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
        private readonly IWindsorContainer _windsorContainer;

        public AtomicReadmodelChangesetConsumerFactory(IWindsorContainer windsorContainer)
        {
            _windsorContainer = windsorContainer;
        }

        public IAtomicReadmodelChangesetConsumer CreateFor(Type atomicReadmodelType)
        {
            var genericType = typeof(AtomicReadmodelChangesetConsumer<>);
            var closedType = genericType.MakeGenericType(new Type[] { atomicReadmodelType });
            return (IAtomicReadmodelChangesetConsumer)_windsorContainer.Resolve(closedType);
        }
    }

    /// <summary>
    /// Simple class to helps handling events for atomic projection
    /// engine.
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    public class AtomicReadmodelChangesetConsumer<TModel> : IAtomicReadmodelChangesetConsumer
         where TModel : IAtomicReadModel
    {
        private readonly IAtomicCollectionWrapper<TModel> _atomicCollectionWrapper;
        private readonly AtomicReadmodelInfoAttribute _atomicReadmodelInfoAttribute;
        private readonly IAtomicReadModelFactory _atomicReadModelFactory;
        private readonly ILogger _logger;

        public AtomicReadmodelChangesetConsumer(
            IAtomicCollectionWrapper<TModel> atomicCollectionWrapper,
            IAtomicReadModelFactory atomicReadModelFactory,
            ILogger logger)
        {
            _atomicCollectionWrapper = atomicCollectionWrapper;
            _atomicReadmodelInfoAttribute = AtomicReadmodelInfoAttribute.GetFrom(typeof(TModel));
            _logger = logger;
            _atomicReadModelFactory = atomicReadModelFactory;
        }

        public AtomicReadmodelInfoAttribute AtomicReadmodelInfoAttribute => _atomicReadmodelInfoAttribute;

        /// <summary>
        /// Handle the events.
        /// </summary>
        /// <param name="changeset"></param>
        public async Task Handle(
            Int64 position,
            Changeset changeset,
            IIdentity identity)
        {
            if (identity.GetType() != _atomicReadmodelInfoAttribute.AggregateIdType)
            {
                return; //this is a changeset not directed to this readmodel, changeset of another aggregate.
            }

            var rm = await _atomicCollectionWrapper.FindOneByIdAsync(identity.AsString()).ConfigureAwait(false);
            Boolean isFirstChangeset = false;
            Boolean readmodelChanged = false;
            if (rm == null)
            {
                //TODO Check if this is the first event.
                rm = _atomicReadModelFactory.Create<TModel>(identity.AsString());
                isFirstChangeset = true;
            }
            try
            {
                readmodelChanged = rm.ProcessChangeset(changeset);
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Error projecting changeset with version {0} with readmodel {1} [{2}] . readmodel will be marked as faulted",
                    changeset.AggregateVersion,
                    AtomicReadmodelInfoAttribute.Name,
                    changeset.Events?.OfType<DomainEvent>()?.FirstOrDefault()?.AggregateId ?? "Unknow aggregate id",
                    ex.Message);
                rm.MarkAsFaulted(position);
                readmodelChanged = true;
                //continue.
            }

            if (readmodelChanged)
            {
                if (isFirstChangeset)
                {
                    await _atomicCollectionWrapper.UpsertAsync(rm).ConfigureAwait(false);
                }
                else
                {
                    await _atomicCollectionWrapper.UpdateAsync(rm).ConfigureAwait(false);
                }
            }
        }
    }
}

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
        public async Task<AtomicReadmodelChangesetConsumerReturnValue> Handle(
            Int64 position,
            Changeset changeset,
            IIdentity identity)
        {
            if (identity.GetType() != _atomicReadmodelInfoAttribute.AggregateIdType)
            {
                return null; //this is a changeset not directed to this readmodel, changeset of another aggregate.
            }

            var rm = await _atomicCollectionWrapper.FindOneByIdAsync(identity.AsString()).ConfigureAwait(false);
            var readmodelCreated = false;
            var readmodelModified = false;
            if (rm == null)
            {
                //TODO Check if this is the first event.
                rm = _atomicReadModelFactory.Create<TModel>(identity.AsString());
                readmodelCreated = true;
            }
            try
            {
                readmodelModified = rm.ProcessChangeset(changeset);
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Error projecting changeset with version {0} with readmodel {1} [{2}] . readmodel will be marked as faulted",
                    changeset.AggregateVersion,
                    AtomicReadmodelInfoAttribute.Name,
                    changeset.Events?.OfType<DomainEvent>()?.FirstOrDefault()?.AggregateId ?? "Unknow aggregate id",
                    ex.Message);
                rm.MarkAsFaulted(position);
                readmodelModified = true;
                //continue.
            }

            if (readmodelModified)
            {
                if (readmodelCreated)
                {
                    await _atomicCollectionWrapper.UpsertAsync(rm).ConfigureAwait(false);
                }
                else
                {
                    await _atomicCollectionWrapper.UpdateAsync(rm).ConfigureAwait(false);
                }
                return new AtomicReadmodelChangesetConsumerReturnValue(rm, readmodelCreated);
            }
            return null;
        }
    }
}

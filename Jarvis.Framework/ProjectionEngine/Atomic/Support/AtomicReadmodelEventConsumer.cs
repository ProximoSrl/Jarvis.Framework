using Castle.Core.Logging;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using NStore.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support
{
    /// <summary>
    /// Simple class to helps handling events for atomic projection
    /// engine.
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    public class AtomicReadmodelProjectorHelper<TModel> : IAtomicReadmodelProjectorHelper
         where TModel : IAtomicReadModel
    {
        private readonly IAtomicCollectionWrapper<TModel> _atomicCollectionWrapper;
        private readonly AtomicReadmodelInfoAttribute _atomicReadmodelInfoAttribute;
        private readonly IAtomicReadModelFactory _atomicReadModelFactory;
        private readonly ILiveAtomicReadModelProcessor _liveAtomicReadModelProcessor;
        private readonly ILogger _logger;

        public AtomicReadmodelProjectorHelper(
            IAtomicCollectionWrapper<TModel> atomicCollectionWrapper,
            IAtomicReadModelFactory atomicReadModelFactory,
            ILiveAtomicReadModelProcessor liveAtomicReadModelProcessor,
            ILogger logger)
        {
            _atomicCollectionWrapper = atomicCollectionWrapper;
            _atomicReadmodelInfoAttribute = AtomicReadmodelInfoAttribute.GetFrom(typeof(TModel));
            _logger = logger;
            _atomicReadModelFactory = atomicReadModelFactory;
            _liveAtomicReadModelProcessor = liveAtomicReadModelProcessor;
        }

        public AtomicReadmodelInfoAttribute AtomicReadmodelInfoAttribute => _atomicReadmodelInfoAttribute;

        /// <summary>
        /// Handle the events.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="changeset"></param>
        /// <param name="identity"></param>
        /// <param name="cancellationToken"></param>
        public async Task<AtomicReadmodelChangesetConsumerReturnValue> Handle(
            Int64 position,
            Changeset changeset,
            IIdentity identity,
            CancellationToken cancellationToken = default)
        {
            if (identity.GetType() != _atomicReadmodelInfoAttribute.AggregateIdType)
            {
                return null; //this is a changeset not directed to this readmodel, changeset of another aggregate.
            }

            var rm = await _atomicCollectionWrapper.FindOneByIdAsync(identity.AsString(), cancellationToken).ConfigureAwait(false);
            var readmodelCreated = false;
            if (rm == null)
            {
                //TODO Check if this is the first event.
                rm = _atomicReadModelFactory.Create<TModel>(identity.AsString());
                readmodelCreated = true;
            }

            bool readmodelModified;
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
                //We mark aggregate as faulted with a special property that tells me is it is a framework exception.
                rm.MarkAsFaulted(position);
                readmodelModified = true;
                //continue.
            }

            if (readmodelModified)
            {
                if (readmodelCreated)
                {
                    await _atomicCollectionWrapper.UpsertAsync(rm, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _atomicCollectionWrapper.UpdateAsync(rm, cancellationToken).ConfigureAwait(false);
                }
                return new AtomicReadmodelChangesetConsumerReturnValue(rm, readmodelCreated);
            }
            else
            {
                //readmodel was not modified, I want only to update some key information so position and aggregate version is update correctly
                await _atomicCollectionWrapper.UpdateVersionAsync(rm, cancellationToken).ConfigureAwait(false);
            }
            return null;
        }

        public async Task<IEnumerable<AtomicReadmodelChangesetConsumerReturnValue>> HandleManyAsync(
            IEnumerable<AtomicReadmodelProjectionItem> items,
            CancellationToken cancellationToken = default)
        {
            var results = new ConcurrentBag<AtomicReadmodelChangesetConsumerReturnValue>();

            await Parallel.ForEachAsync(
                items,
                cancellationToken,
                async (item, ct) =>
                {
                    var result = await Handle(item.Position, item.Changeset, item.Identity, ct).ConfigureAwait(false);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }).ConfigureAwait(false);

            return results;
        }

        public async Task FullProjectAsync(IIdentity identity, CancellationToken cancellationToken = default)
        {
            var rm = await _liveAtomicReadModelProcessor.ProcessAsync<TModel>(identity.AsString(), Int64.MaxValue, cancellationToken).ConfigureAwait(false);
            if (rm != null)
            {
                await _atomicCollectionWrapper.UpsertAsync(rm, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

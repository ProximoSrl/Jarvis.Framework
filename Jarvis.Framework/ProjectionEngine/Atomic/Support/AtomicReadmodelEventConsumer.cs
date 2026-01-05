using Castle.Core.Logging;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using MongoDB.Driver.Linq;
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

            bool readmodelModified = InnerDispatchChangesetOnReadmodel(position, changeset, rm);

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

        /// <summary>
        /// This will dispatch the changeset on the readmodel and handle possible exceptions.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="changeset"></param>
        /// <param name="rm"></param>
        /// <returns></returns>
        private bool InnerDispatchChangesetOnReadmodel(long position, Changeset changeset, TModel rm)
        {
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

            return readmodelModified;
        }

        public async Task<IEnumerable<AtomicReadmodelChangesetConsumerReturnValue>> HandleManyAsync(
            IEnumerable<AtomicReadmodelProjectionItem> items,
            CancellationToken cancellationToken = default)
        {
            // Validate that each identity appears only once in the batch
            var identityGroups = items
                .GroupBy(i => i.Identity.AsString())
                .Where(g => g.Count() > 1)
                .ToList();

            if (identityGroups.Count > 0)
            {
                var duplicateIdentities = string.Join(", ", identityGroups.Select(g => $"{g.Key} (count: {g.Count()})"));
                throw new InvalidOperationException(
                    $"HandleManyAsync does not support multiple changesets for the same identity in a single batch. " +
                    $"Each identity must appear only once. Duplicate identities found: {duplicateIdentities}");
            }

            // use the _atomicReadmodelInfoAttribute to filter only relevant items based on how it is used on the standard handle method
            var itemsToProcess = items
                .Where(item => item.Identity.GetType() == _atomicReadmodelInfoAttribute.AggregateIdType)
                .ToList();

            if (itemsToProcess.Count == 0)
            {
                return Enumerable.Empty<AtomicReadmodelChangesetConsumerReturnValue>();
            }

            //ok we have some work to do.
            var results = new ConcurrentBag<AtomicReadmodelChangesetConsumerReturnValue>();

            //we need to load all the readmodels first in a dictionary
            var allId = itemsToProcess.Select(i => i.Identity.AsString()).ToList();
            var readmodels = await _atomicCollectionWrapper.AsQueryable().Where(rm => allId.Contains(rm.Id)).ToListAsync(cancellationToken: cancellationToken);
            var rmDictionary = new ConcurrentDictionary<string, ReadModelProcessingInfo>();
            foreach (var item in readmodels)
            {
                rmDictionary[item.Id] = new ReadModelProcessingInfo
                {
                    ReadModel = item,
                    Processed = false,
                    CreatedForFirstTime = false
                };
            }

            await Parallel.ForEachAsync(
                itemsToProcess,
                cancellationToken,
                async (item, ct) =>
                {
                    TModel readmodel;
                    ReadModelProcessingInfo readmodelInfo;
                    bool createdForFirstTime;
                    if (!rmDictionary.TryGetValue(item.Identity.AsString(), out readmodelInfo))
                    {
                        readmodel = _atomicReadModelFactory.Create<TModel>(item.Identity.AsString());
                        createdForFirstTime = true;
                        readmodelInfo = new ReadModelProcessingInfo
                        {
                            ReadModel = readmodel,
                            Processed = false,
                            CreatedForFirstTime = true
                        };
                        rmDictionary[item.Identity.AsString()] = readmodelInfo;
                    }
                    else
                    {
                        readmodel = readmodelInfo.ReadModel;
                        createdForFirstTime = false;
                    }

                    bool processed = InnerDispatchChangesetOnReadmodel(item.Position, item.Changeset, readmodel);
                    readmodelInfo.Processed = processed;

                    //Mimic the single handle behavior: only add to results if processed
                    if (processed)
                    {
                        results.Add(new AtomicReadmodelChangesetConsumerReturnValue(readmodel, createdForFirstTime));
                    }

                }).ConfigureAwait(false);

            //now we will persist all readmodels
            var allReadmodels = rmDictionary.Values.Select(v => v.ReadModel).ToList();
            await _atomicCollectionWrapper.UpsertBatchAsync(allReadmodels, cancellationToken).ConfigureAwait(false);
           
            return results;
        }

        private class ReadModelProcessingInfo
        {
            public TModel ReadModel { get; set; }
            public bool Processed { get; set; }
            public bool CreatedForFirstTime { get; set; }
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

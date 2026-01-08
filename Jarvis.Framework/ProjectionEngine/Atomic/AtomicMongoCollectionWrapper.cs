using App.Metrics;
using App.Metrics.Counter;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Shared.Support;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    /// <inheritdoc />
    public class AtomicMongoCollectionWrapper<TModel> : IAtomicCollectionWrapper<TModel>
        where TModel : class, IAtomicReadModel
    {
        private const string ConcurrencyException = "E1100";

        private readonly IMongoCollection<TModel> _collection;
        private readonly IMongoCollection<TModel> _collectionOnSecondary;

        private readonly Int32 _actualVersion;
        private readonly ILiveAtomicReadModelProcessor _liveAtomicReadModelProcessor;

        internal const int MaxNumberOfRetryToReprojectFaultedReadmodels = 3;

        protected readonly ILogger _logger;

        private static readonly CounterOptions FindOneByIdCounter = new CounterOptions()
        {
            Name = "AtomicRmFindOneByIdr",
            MeasurementUnit = Unit.Calls
        };

        private static readonly CounterOptions QueryCounter = new CounterOptions()
        {
            Name = "AtomicRmQuery",
            MeasurementUnit = Unit.Calls
        };

        private static readonly CounterOptions QueryCounterSecondary = new CounterOptions()
        {
            Name = "AtomicRmQueryOnSecondary",
            MeasurementUnit = Unit.Calls
        };

        private static readonly CounterOptions FindOneByIdAndCachupCounter = new CounterOptions()
        {
            Name = "AtomicRmFindOneByIdAndCachup",
            MeasurementUnit = Unit.Calls
        };

        private static readonly CounterOptions FindOneByIdAtCheckpointCachupCounter = new CounterOptions()
        {
            Name = "AtomicRmFindOneByIdAtCheckpointCachup",
            MeasurementUnit = Unit.Calls
        };

        private static ReadPreference secondaryReadPreference = new ReadPreference(ReadPreferenceMode.SecondaryPreferred);

        public AtomicMongoCollectionWrapper(
            IMongoDatabase readmodelDb,
            IAtomicReadModelFactory atomicReadModelFactory,
            ILiveAtomicReadModelProcessor liveAtomicReadModelProcessor,
            ILogger logger)
        {
            _logger = logger;
            _liveAtomicReadModelProcessor = liveAtomicReadModelProcessor;

            var collectionName = CollectionNames.GetCollectionName<TModel>();
            _collection = readmodelDb.GetCollection<TModel>(collectionName);
            _collectionOnSecondary = readmodelDb.GetCollection<TModel>(collectionName).WithReadPreference(secondaryReadPreference);

            var allIndex = _collection.GetIndexNames();

            DropOldIndexes(allIndex);

            if (!allIndex.Contains("ReadModelVersionForFixer2"))
            {
                //This is the base index that will be used from the fixer to find 
                //readmodel that are old and needs to be fixed.
                _logger.InfoFormat($"About to create index ReadModelVersionForFixer2 for Readmodel collection {0}", _collection.CollectionNamespace.CollectionName);
                _collection.Indexes.CreateOne(
                   new CreateIndexModel<TModel>(
                       Builders<TModel>.IndexKeys
                           .Ascending(_ => _.ProjectedPosition)
                           .Ascending(_ => _.ReadModelVersion)
                           .Ascending(_ => _.Id),
                       new CreateIndexOptions()
                       {
                           Name = "ReadModelVersionForFixer2",
                           Background = false
                       }
                   )
                );
            }

            _actualVersion = atomicReadModelFactory.GetReamdodelVersion(typeof(TModel));
        }

        private void DropOldIndexes(IReadOnlyCollection<string> allIndex)
        {
            if (allIndex.Contains("ReadModelVersion"))
            {
                try
                {
                    //Drop the old readmodel version index.
                    _logger.InfoFormat("Dropping old index ReadModelVersion from readmodel collection {0}", _collection.CollectionNamespace.CollectionName);
                    _collection.Indexes.DropOne("ReadModelVersion");
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "Error dropping ReadModelVersion index from readmodel collection {0}", _collection.CollectionNamespace.CollectionName);
                    //Ignore all exceptions, if we cannot drop the index we can proceed
                }
            }
            if (allIndex.Contains("ProjectedPosition"))
            {
                try
                {
                    //Auto create the basic index you need
                    _logger.InfoFormat($"Drop old ProjectedPositionIndex since we have ReadModelVersionForFixer2 for collection {0}", _collection.CollectionNamespace.CollectionName);
                    _collection.Indexes.DropOne("ProjectedPosition");
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "Error dropping ProjectedPosition Index from readmodel collection {0}", _collection.CollectionNamespace.CollectionName);
                    //Ignore all exceptions, if we cannot drop the index we can proceed
                }
            }
            if (allIndex.Contains("ReadModelVersionForFixer"))
            {
                try
                {
                    //Drop the old readmodel version index.
                    _logger.InfoFormat("Dropping old index ReadModelVersionForFixer from readmodel collection {0}", _collection.CollectionNamespace.CollectionName);
                    _collection.Indexes.DropOne("ReadModelVersionForFixer");
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "Error dropping ReadModelVersionForFixer index from readmodel collection {0}", _collection.CollectionNamespace.CollectionName);
                    //Ignore all exceptions, if we cannot drop the index we can proceed
                }
            }
        }

        /// <inheritdoc />
        public IQueryable<TModel> AsQueryable()
        {
            JarvisFrameworkMetricsHelper.Counter.Increment(QueryCounter, 1, typeof(TModel).Name);
            return _collection.AsQueryable();
        }

        public IQueryable<TModel> AsQueryableSecondaryPreferred()
        {
            JarvisFrameworkMetricsHelper.Counter.Increment(QueryCounterSecondary, 1, typeof(TModel).Name);
            return _collectionOnSecondary.AsQueryable();
        }

        /// <inheritdoc />
        public async Task<TModel> FindOneByIdAsync(String id, CancellationToken cancellationToken = default)
        {
            TModel rm = null;
            Boolean fixableExceptions = false;
            JarvisFrameworkMetricsHelper.Counter.Increment(FindOneByIdCounter, 1, typeof(TModel).Name);

            //try to read, but intercept a format exception if someone changed binary
            //serialization on mongodb.
            try
            {
                rm = await _collection.FindOneByIdAsync(id, cancellationToken).ConfigureAwait(false);

                //if readmodel is faulted we can retry up to MaxNumberOfRetryToReprojectFaultedReadmodels to reproject.
                if (rm?.Faulted == true && rm.FaultRetryCount < MaxNumberOfRetryToReprojectFaultedReadmodels) fixableExceptions = true;
            }
            catch (FormatException)
            {
                fixableExceptions = true;
            }

            //ok check if we need rebuild in memory, it happens when the RM changed version
            //or if we have a fixable exception.
            if (fixableExceptions || (rm != null && rm.ReadModelVersion != _actualVersion))
            {
                //We need to update mongo only if the actual signature is greater
                //than the old signature that we found on the database.
                Boolean shouldSave = fixableExceptions || rm.ReadModelVersion < _actualVersion;

                //houston, we have a problem, database readmodel is out of sync.
                TModel fixedRm = null;

                try
                {
                    fixedRm = await _liveAtomicReadModelProcessor.ProcessAsync<TModel>(id, Int32.MaxValue, cancellationToken).ConfigureAwait(false);
                    //Save if we have a newer readmodel
                    if (shouldSave && fixedRm != null)
                    {
                        await UpdateAsync(fixedRm, cancellationToken).ConfigureAwait(false);
                    }
                    rm = fixedRm;
                }
                catch (Exception)
                {
                    //ok we cannot fix the record, we have some nasty problem, we mark the readmodel as faulted an return the original
                    //readmodel, if reamdodel is null, we need to throw, because we cannot read anything, but if the readmodel
                    //was correctly read and was already fault, we need to tolerate the error and move on
                    if (rm?.Faulted == true)
                    {
                        //we were fixing a readmodel that was already faulted, we return the already faulted readmodel but increment failure
                        await IncrementFailureCount(rm, cancellationToken);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return rm;
        }

        private Task IncrementFailureCount(TModel rm, CancellationToken cancellationToken = default)
        {
            return _collection.FindOneAndUpdateAsync(
                Builders<TModel>.Filter.Eq("_id", rm.Id),
                Builders<TModel>.Update.Inc(d => d.FaultRetryCount, 1),
                cancellationToken: cancellationToken);
        }		/// <inheritdoc />
		public async Task<TModel> FindOneByIdAndCatchupAsync(string id, CancellationToken cancellationToken = default)
        {
            var rm = await FindOneByIdAsync(id, cancellationToken).ConfigureAwait(false);
            JarvisFrameworkMetricsHelper.Counter.Increment(FindOneByIdAndCachupCounter, 1, typeof(TModel).Name);
            if (rm == null)
            {
                //Try to project a new readmodel
                rm = await _liveAtomicReadModelProcessor.ProcessAsync<TModel>(id, Int64.MaxValue).ConfigureAwait(false);
                if (rm?.AggregateVersion == 0)
                {
                    //we have an aggregate with no commit, just return null.
                    return null;
                }
                return rm; //Return new projected readmodel
            }
            await _liveAtomicReadModelProcessor.CatchupAsync(rm).ConfigureAwait(false);
            return rm;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<TModel>> FindManyAsync(System.Linq.Expressions.Expression<Func<TModel, bool>> filter, bool fixVersion = false, CancellationToken cancellationToken = default)
        {
            JarvisFrameworkMetricsHelper.Counter.Increment(QueryCounter, 1, typeof(TModel).Name);
            var rms = await _collection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!fixVersion)
            {
                return rms.ToList();
            }

            var rmsResult = new List<TModel>();
            foreach (var rm in rms.ToEnumerable())
            {
                if (rm.ReadModelVersion != _actualVersion)
                {
                    rmsResult.Add(await FindOneByIdAsync(rm.Id, cancellationToken).ConfigureAwait(false));
                }
                else
                {
                    rmsResult.Add(rm);
                }
            }
            return rmsResult;
        }

        /// <inheritdoc />
        public Task UpsertForceAsync(TModel model, CancellationToken cancellationToken = default)
        {
            return UpsertAsync(model, false, cancellationToken);
        }

        /// <inheritdoc />
        public Task UpsertAsync(TModel model, CancellationToken cancellationToken = default)
        {
            return UpsertAsync(model, true, cancellationToken);
        }

        /// <summary>
        /// This can be tricky, we need to save and honor idempotency in the more efficient
        /// way possible
        /// </summary>
        /// <param name="model"></param>
        /// <param name="performAggregateVersionCheck"></param>
        /// <returns></returns>
        private async Task UpsertAsync(TModel model, Boolean performAggregateVersionCheck, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(model.Id))
            {
                throw new CollectionWrapperException("Cannot save readmodel, Id property not initialized");
            }

            if (model.ModifiedWithExtraStreamEvents)
            {
                return; //nothing should be done, the readmodel is not in a peristable state
            }

            //we need to save the object, we first try to find and replace, if the operation 
            //fails we have two distinct situation, the insert and the update
            var existing = await _collection.Find(
                    Builders<TModel>.Filter.Eq(_ => _.Id, model.Id)
                ).Project(
                    Builders<TModel>.Projection
                        .Include(_ => _.AggregateVersion)
                        .Include(_ => _.ReadModelVersion)
                ).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false); if (performAggregateVersionCheck
                    && existing != null
                    && (
                        existing["AggregateVersion"].AsInt64 > model.AggregateVersion
                    || (
                            existing["AggregateVersion"].AsInt64 == model.AggregateVersion
                            && existing["ReadModelVersion"].AsInt32 >= model.ReadModelVersion)
                    )
                )
            {
                return; //nothing to do, we already have a model with higer version
            }

            //ok I'll do an upsert, the condition is different, we need to try to insert
            //if we do not have an existing element in the database.
            if (existing == null)
            {
                //we do not have the readmodel, we can insert, but we check for concurrency to be safe
                //in the case we have a concurrent insert.
                try
                {
                    await _collection.InsertOneAsync(model, cancellationToken: cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (MongoException mex)
                {
                    if (!mex.Message.Contains(ConcurrencyException))
                    {
                        throw;
                    }
                }
            }

            //ok if we reach here, a record is already present
            //or we have a concurrent exception because two threads entered so we need to perform a standard update
            //and let idempotency rules work.
            await UpdateAsync(model, performAggregateVersionCheck, cancellationToken).ConfigureAwait(false);
        }

        public Task UpdateAsync(TModel model, CancellationToken cancellationToken = default)
        {
            return UpdateAsync(model, true, cancellationToken);
        }

        public Task UpdateAsync(TModel model, Boolean performAggregateVersionCheck, CancellationToken cancellationToken = default)
        {
            if (model.ModifiedWithExtraStreamEvents)
            {
                return Task.CompletedTask;
            }

            var filter = Builders<TModel>.Filter.And(
                    Builders<TModel>.Filter.Eq(_ => _.Id, model.Id),
                    Builders<TModel>.Filter.Lte(_ => _.ReadModelVersion, model.ReadModelVersion)
                );

            if (performAggregateVersionCheck)
            {
                filter &= Builders<TModel>.Filter.Or(
                        Builders<TModel>.Filter.Lt(_ => _.AggregateVersion, model.AggregateVersion),
                        Builders<TModel>.Filter.Lt(_ => _.ReadModelVersion, model.ReadModelVersion)
                    );
            }

            return _collection.FindOneAndReplaceAsync(
                filter,
                model, new FindOneAndReplaceOptions<TModel>()
                {
                    ReturnDocument = ReturnDocument.After
                },
                cancellationToken);
        }		/// <inheritdoc />
		public async Task<TModel> FindOneByIdAtCheckpointAsync(string id, long chunkPosition, CancellationToken cancellationToken = default)
        {
            JarvisFrameworkMetricsHelper.Counter.Increment(FindOneByIdAtCheckpointCachupCounter, 1, typeof(TModel).Name);
            //TODO: cache somewhat readmodel in some cache database to avoid rebuilding always at version in memory.
            var readmodel = await _liveAtomicReadModelProcessor.ProcessUntilChunkPositionAsync<TModel>(id, chunkPosition, cancellationToken);
            if (readmodel?.AggregateVersion == 0)
            {
                return null;
            }

            return readmodel;
        }

        public async Task UpdateVersionAsync(TModel model, CancellationToken cancellationToken = default)
        {
            var filter = Builders<TModel>.Filter.And(
                Builders<TModel>.Filter.Eq(_ => _.Id, model.Id)
            );

            var update = Builders<TModel>.Update
                .Set(m => m.ProjectedPosition, model.ProjectedPosition)
                .Set(m => m.AggregateVersion, model.AggregateVersion)
                .Set(m => m.LastProcessedVersions, model.LastProcessedVersions);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            if (result.ModifiedCount == 0)
            {
                //ok the readmodel is not present on disk, this is a rare situation when the
                //readmodel was created in memory for the first time, and all received changeset
                //does not change the readmodel, we will persist.
                await UpsertAsync(model, false, cancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task UpdateVersionBatchAsync(IEnumerable<TModel> models, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(models);

            // Convert to list to avoid multiple enumerations
            var modelsList = models.ToList();

            if (modelsList.Count == 0)
            {
                return; // Nothing to process
            }

            // Validate all models have Ids and check for duplicates
            var seenIds = new HashSet<string>();
            var duplicateIds = new List<string>();

            foreach (var model in modelsList)
            {
                if (String.IsNullOrEmpty(model.Id))
                {
                    throw new CollectionWrapperException($"Cannot update readmodel of type {typeof(TModel).Name}, Id property not initialized");
                }

                if (!seenIds.Add(model.Id))
                {
                    duplicateIds.Add(model.Id);
                }
            }

            if (duplicateIds.Count > 0)
            {
                var duplicatesStr = string.Join(", ", duplicateIds.Distinct());
                throw new CollectionWrapperException($"Duplicate readmodel Ids found in batch: {duplicatesStr}");
            }

            // Filter out models that should not be persisted
            var validModels = modelsList.Where(m => !m.ModifiedWithExtraStreamEvents).ToList();

            if (validModels.Count == 0)
            {
                return; // All models were filtered out
            }

            // Query which models already exist
            var ids = validModels.Select(m => m.Id).ToList();
            var existingFilter = Builders<TModel>.Filter.In(_ => _.Id, ids);
            var existingIds = await _collection.Find(existingFilter)
                .Project(Builders<TModel>.Projection.Include(_ => _.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var existingIdSet = new HashSet<string>(existingIds.Select(doc => doc["_id"].AsString));

            // Separate models into existing (for update) and missing (for insert)
            var modelsToUpdate = validModels.Where(m => existingIdSet.Contains(m.Id)).ToList();
            var modelsToInsert = validModels.Where(m => !existingIdSet.Contains(m.Id)).ToList();

            // Build bulk update operations for existing models
            if (modelsToUpdate.Count > 0)
            {
                var bulkOps = new List<WriteModel<TModel>>();

                foreach (var model in modelsToUpdate)
                {
                    var filter = Builders<TModel>.Filter.Eq(_ => _.Id, model.Id);
                    var update = Builders<TModel>.Update
                        .Set(m => m.ProjectedPosition, model.ProjectedPosition)
                        .Set(m => m.AggregateVersion, model.AggregateVersion)
                        .Set(m => m.LastProcessedVersions, model.LastProcessedVersions);

                    bulkOps.Add(new UpdateOneModel<TModel>(filter, update));
                }

                await _collection.BulkWriteAsync(bulkOps, new BulkWriteOptions { IsOrdered = false }, cancellationToken).ConfigureAwait(false);
            }

            // Insert missing models (fallback behavior from UpdateVersionAsync)
            if (modelsToInsert.Count > 0)
            {
                try
                {
                    await _collection.InsertManyAsync(modelsToInsert, new InsertManyOptions { IsOrdered = false }, cancellationToken).ConfigureAwait(false);
                }
                catch (MongoException mex)
                {
                    // Handle concurrent inserts - if another thread inserted between our check and insert,
                    // we can safely ignore duplicate key errors
                    if (!mex.Message.Contains(ConcurrencyException))
                    {
                        throw;
                    }
                    // Duplicate key errors are expected in concurrent scenarios and can be ignored
                }
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new CollectionWrapperException("Cannot delete readmodel, Id is null or empty");
            }

            var filter = Builders<TModel>.Filter.Eq(_ => _.Id, id);
            await _collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task UpsertBatchAsync(IEnumerable<TModel> models, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(models);

            // Convert to list to avoid multiple enumerations and to allow checking count
            var modelsList = models.ToList();
            
            if (modelsList.Count == 0)
            {
                return; // Nothing to process
            }

            // Validate all models have Ids and check for duplicates
            var seenIds = new HashSet<string>();
            var duplicateIds = new List<string>();
            
            foreach (var model in modelsList)
            {
                if (String.IsNullOrEmpty(model.Id))
                {
                    throw new CollectionWrapperException($"Cannot save readmodel of type {typeof(TModel).Name}, Id property not initialized");
                }

                if (!seenIds.Add(model.Id))
                {
                    duplicateIds.Add(model.Id);
                }
            }

            if (duplicateIds.Count > 0)
            {
                var duplicatesStr = string.Join(", ", duplicateIds.Distinct());
                throw new CollectionWrapperException($"Duplicate readmodel Ids found in batch: {duplicatesStr}");
            }

            // Filter out models that should not be persisted
            var validModels = modelsList.Where(m => !m.ModifiedWithExtraStreamEvents).ToList();

            if (validModels.Count == 0)
            {
                return; // All models were filtered out
            }

            // For efficiency, we'll build a dictionary of existing models to check versions
            var ids = validModels.Select(m => m.Id).ToList();
            var existingFilter = Builders<TModel>.Filter.In(_ => _.Id, ids);
            var existingModels = await _collection.Find(existingFilter)
                .Project(Builders<TModel>.Projection
                    .Include(_ => _.Id)
                    .Include(_ => _.AggregateVersion)
                    .Include(_ => _.ReadModelVersion))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // Build a dictionary for quick lookup
            var existingVersions = existingModels.ToDictionary(
                doc => doc["_id"].AsString,
                doc => new { AggregateVersion = doc["AggregateVersion"].AsInt64, ReadModelVersion = doc["ReadModelVersion"].AsInt32 }
            );

            // Build the bulk write operations
            var bulkOps = new List<WriteModel<TModel>>();

            foreach (var model in validModels)
            {
                // Check if we should skip this model due to version conflicts
                if (existingVersions.TryGetValue(model.Id, out var existing))
                {
                    // Skip if existing version is higher or equal
                    if (existing.AggregateVersion > model.AggregateVersion
                        || (existing.AggregateVersion == model.AggregateVersion && existing.ReadModelVersion >= model.ReadModelVersion))
                    {
                        continue; // Skip this model, already have newer version
                    }

                    // Build filter for replace: must match id and have lower version
                    var replaceFilter = Builders<TModel>.Filter.And(
                        Builders<TModel>.Filter.Eq(_ => _.Id, model.Id),
                        Builders<TModel>.Filter.Lte(_ => _.ReadModelVersion, model.ReadModelVersion),
                        Builders<TModel>.Filter.Or(
                            Builders<TModel>.Filter.Lt(_ => _.AggregateVersion, model.AggregateVersion),
                            Builders<TModel>.Filter.Lt(_ => _.ReadModelVersion, model.ReadModelVersion)
                        )
                    );

                    bulkOps.Add(new ReplaceOneModel<TModel>(replaceFilter, model));
                }
                else
                {
                    // Model doesn't exist, use upsert to insert it
                    var upsertFilter = Builders<TModel>.Filter.Eq(_ => _.Id, model.Id);
                    bulkOps.Add(new ReplaceOneModel<TModel>(upsertFilter, model) { IsUpsert = true });
                }
            }

            if (bulkOps.Count == 0)
            {
                return; // All models were skipped due to version checks
            }

            // Execute the bulk write operation
            await _collection.BulkWriteAsync(bulkOps, new BulkWriteOptions { IsOrdered = false }, cancellationToken).ConfigureAwait(false);
        }
    }
}

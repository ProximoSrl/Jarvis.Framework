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
        public async Task<TModel> FindOneByIdAsync(String id)
		{
			TModel rm = null;
			Boolean fixableExceptions = false;
			JarvisFrameworkMetricsHelper.Counter.Increment(FindOneByIdCounter, 1, typeof(TModel).Name);

			//try to read, but intercept a format exception if someone changed binary
			//serialization on mongodb.
			try
			{
				rm = await _collection.FindOneByIdAsync(id).ConfigureAwait(false);

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
					fixedRm = await _liveAtomicReadModelProcessor.ProcessAsync<TModel>(id, Int32.MaxValue).ConfigureAwait(false);
					//Save if we have a newer readmodel
					if (shouldSave && fixedRm != null)
					{
						await UpdateAsync(fixedRm).ConfigureAwait(false);
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
						await IncrementFailureCount(rm);
					}
					else
					{
						throw;
					}
				}
			}

			return rm;
		}

		private Task IncrementFailureCount(TModel rm)
		{
			return _collection.FindOneAndUpdateAsync(
				Builders<TModel>.Filter.Eq("_id", rm.Id),
				Builders<TModel>.Update.Inc(d => d.FaultRetryCount, 1));
		}

		/// <inheritdoc />
		public async Task<TModel> FindOneByIdAndCatchupAsync(string id)
		{
			var rm = await FindOneByIdAsync(id).ConfigureAwait(false);
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
		public async Task<IReadOnlyCollection<TModel>> FindManyAsync(System.Linq.Expressions.Expression<Func<TModel, bool>> filter, bool fixVersion = false)
		{
			JarvisFrameworkMetricsHelper.Counter.Increment(QueryCounter, 1, typeof(TModel).Name);
			var rms = await _collection.FindAsync(filter).ConfigureAwait(false);

			if (!fixVersion)
			{
				return rms.ToList();
			}

			var rmsResult = new List<TModel>();
			foreach (var rm in rms.ToEnumerable())
			{
				if (rm.ReadModelVersion != _actualVersion)
				{
					rmsResult.Add(await FindOneByIdAsync(rm.Id).ConfigureAwait(false));
				}
				else
				{
					rmsResult.Add(rm);
				}
			}
			return rmsResult;
		}

		/// <inheritdoc />
		public Task UpsertForceAsync(TModel model)
		{
			return UpsertAsync(model, false);
		}

		/// <inheritdoc />
		public Task UpsertAsync(TModel model)
		{
			return UpsertAsync(model, true);
		}

		/// <summary>
		/// This can be tricky, we need to save and honor idempotency in the more efficient
		/// way possible
		/// </summary>
		/// <param name="model"></param>
		/// <param name="performAggregateVersionCheck"></param>
		/// <returns></returns>
		private async Task UpsertAsync(TModel model, Boolean performAggregateVersionCheck)
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
				).SingleOrDefaultAsync().ConfigureAwait(false);

			if (performAggregateVersionCheck
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
                    await _collection.InsertOneAsync(model).ConfigureAwait(false);
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

			//ok if we reach here, we incurr in concurrent exception, a record is alreay
			//present and inserted between the two call, we need to update and let the idempotency avoid corruption
			await UpdateAsync(model, performAggregateVersionCheck).ConfigureAwait(false);
		}

		public Task UpdateAsync(TModel model)
		{
			return UpdateAsync(model, true);
		}

		public Task UpdateAsync(TModel model, Boolean performAggregateVersionCheck)
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
				});
		}

		/// <inheritdoc />
		public async Task<TModel> FindOneByIdAtCheckpointAsync(string id, long chunkPosition)
		{
			JarvisFrameworkMetricsHelper.Counter.Increment(FindOneByIdAtCheckpointCachupCounter, 1, typeof(TModel).Name);
			//TODO: cache somewhat readmodel in some cache database to avoid rebuilding always at version in memory.
			var readmodel = await _liveAtomicReadModelProcessor.ProcessAsyncUntilChunkPosition<TModel>(id, chunkPosition);
			if (readmodel?.AggregateVersion == 0)
			{
				return null;
			}

			return readmodel;
		}

        public async Task UpdateVersionAsync(TModel model)
        {
            var filter = Builders<TModel>.Filter.And(
                Builders<TModel>.Filter.Eq(_ => _.Id, model.Id)
            );

            var update = Builders<TModel>.Update
                .Set(m => m.ProjectedPosition, model.ProjectedPosition)
                .Set(m => m.AggregateVersion, model.AggregateVersion)
                .Set(m => m.LastProcessedVersions, model.LastProcessedVersions);

            var result = await _collection.UpdateOneAsync(filter, update);
            if (result.ModifiedCount == 0)
            {
                //ok the readmodel is not present on disk, this is a rare situation when the
                //readmodel was created in memory for the first time, and all received changeset
                //does not change the readmodel, we will persist.
                await UpsertAsync(model, false);
            }
        }
    }
}

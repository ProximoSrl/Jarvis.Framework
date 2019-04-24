using Castle.Core.Logging;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    public class AtomicMongoCollectionWrapper<TModel> :
          IAtomicMongoCollectionWrapper<TModel>
        where TModel : class, IAtomicReadModel
    {
        private const string ConcurrencyException = "E1100";

        private readonly IMongoCollection<TModel> _collection;
        private readonly Int32 _actualVersion;
        private readonly ILiveAtomicReadModelProcessor _liveAtomicReadModelProcessor;

        public AtomicMongoCollectionWrapper(
            IMongoDatabase readmodelDb,
            IAtomicReadModelFactory atomicReadModelFactory,
            ILiveAtomicReadModelProcessor liveAtomicReadModelProcessor)
        {
            _liveAtomicReadModelProcessor = liveAtomicReadModelProcessor;

            var collectionName = CollectionNames.GetCollectionName<TModel>();
            _collection = readmodelDb.GetCollection<TModel>(collectionName);

            Logger = NullLogger.Instance;

            //Auto create the basic index you need
            _collection.Indexes.CreateOne(
                new CreateIndexModel<TModel>(
                    Builders<TModel>.IndexKeys
                        .Ascending(_ => _.ProjectedPosition),
                    new CreateIndexOptions()
                    {
                        Name = "ProjectedPosition",
                        Background = false
                    }
                )
             );

            _collection.Indexes.CreateOne(
               new CreateIndexModel<TModel>(
                   Builders<TModel>.IndexKeys
                       .Ascending(_ => _.ReadModelVersion),
                   new CreateIndexOptions()
                   {
                       Name = "ReadModelVersion",
                       Background = false
                   }
               )
            );

            _actualVersion = atomicReadModelFactory.GetReamdodelVersion(typeof(TModel));
        }

        /// <summary>
        /// Initialized by castle
        /// </summary>
        public ILogger Logger { get; set; }

        public IQueryable<TModel> AsQueryable()
        {
            return _collection.AsQueryable();
        }

        public async Task<TModel> FindOneByIdAsync(String id)
        {
            TModel rm = null;
            Boolean fixableExceptions = false;
            //try to read, but intercept a format exception if someone changed binary
            //serialization on mongodb.
            try
            {
                rm = await _collection.FindOneByIdAsync(id).ConfigureAwait(false);
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
                rm = await _liveAtomicReadModelProcessor.ProcessAsync<TModel>(id, Int32.MaxValue).ConfigureAwait(false);

                //Save if we have a newer readmodel
                if (shouldSave)
                {
                    await UpdateAsync(rm).ConfigureAwait(false);
                }
            }

            return rm;
        }

        public async Task<IEnumerable<TModel>> FindManyAsync(System.Linq.Expressions.Expression<Func<TModel, bool>> filter, bool fixVersion = false)
        {
            var rms = await _collection.FindAsync(filter).ConfigureAwait(false);

            if (!fixVersion)
            {
                return rms.ToEnumerable();
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

        /// <summary>
        /// Perform an upsert without <see cref="AbstractAtomicReadModel.AggregateVersion"/> check.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public Task UpsertForceAsync(TModel model)
        {
            return UpsertAsync(model, false);
        }

        public Task UpsertAsync(TModel model)
        {
            return UpsertAsync(model, true);
        }

        /// <summary>
        /// This can be tricky, we need to save and honor idempotency in the more efficient
        /// way possible
        /// </summary>
        /// <param name="model"></param>
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

            //ok if we reach here, we incurr in concurrent exception, a record is alreay
            //present and inserted between the 
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

        public IMongoCollection<TModel> Collection => _collection;

        public IMongoQueryable AsMongoQueryable()
        {
            return _collection.AsQueryable();
        }
    }
}

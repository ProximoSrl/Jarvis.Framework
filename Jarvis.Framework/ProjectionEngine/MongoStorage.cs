using Castle.Core.Logging;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.Support;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class MongoStorage<TModel, TKey> : IMongoStorage<TModel, TKey> where TModel : class, IReadModelEx<TKey>
    {
        private readonly IMongoCollection<TModel> _collection;

        public ILogger Logger { get; set; }

        public MongoStorage(IMongoCollection<TModel> collection)
        {
            _collection = collection;
            Logger = NullLogger.Instance;
        }

        public async Task<Boolean> IndexExistsAsync(string name)
        {
            using (var cursor = await _collection.Indexes.ListAsync().ConfigureAwait(false))
            {
                var list = await cursor.ToListAsync().ConfigureAwait(false);
                return list.Any(i => i["name"].AsString == name);
            }
        }

        public async Task DropIndexAsync(String name)
        {
            var allIndexes = await _collection.GetIndexNamesAsync();
            var existing = allIndexes.Any(d => d.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing)
            {
                await _collection.Indexes.DropOneAsync(name);
            }
        }

        /// <summary>
        /// Creates an index, but ignore any error (it will only log them) to be used in projections.
        /// If a projections failed to create an index it will stop and this is not the best approach,
        /// we prefer to ignore indexing and log the error.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="keys"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public async Task CreateIndexAsync(String name, IndexKeysDefinition<TModel> keys, CreateIndexOptions options = null)
        {
            options = options ?? new CreateIndexOptions();
            options.Name = name;

            //there is the possibility that create index trow if an index with same name and 
            //different options/keys is created
            Boolean indexCreated = await InnerCreateIndexAsync(name, keys, options).ConfigureAwait(false);

            if (!indexCreated)
            {
                try
                {
                    //if we reach here, index was not created, probably it is existing and with different keys.
                    var indexExists = await _collection.IndexExistsAsync(name).ConfigureAwait(false);
                    if (indexExists)
                    {
                        await _collection.Indexes.DropOneAsync(name).ConfigureAwait(false);
                        await InnerCreateIndexAsync(name, keys, options).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.ErrorFormat(ex, "Unable to create index {0}", name);
                }
            }
            else
            {
                Logger.ErrorFormat("Unable to create index {0}", name);
            }
        }

        private async Task<bool> InnerCreateIndexAsync(string name, IndexKeysDefinition<TModel> keys, CreateIndexOptions options)
        {
            try
            {
                await _collection.Indexes.CreateOneAsync(keys, options).ConfigureAwait(false);
                return true;
            }
            catch (MongoCommandException ex)
            {
                Logger.ErrorFormat(ex, "Error creating index {0} - {1}", name, ex.Message);
            }

            return false;
        }

        public async Task InsertBatchAsync(IEnumerable<TModel> values)
        {
            await _collection.InsertManyAsync(values).ConfigureAwait(false);
        }

        public IQueryable<TModel> All
        {
            get { return _collection.AsQueryable(); }
        }

        public Task<TModel> FindOneByIdAsync(TKey id)
        {
            return _collection.FindOneByIdAsync(id);
        }

        public TModel FindOneById(TKey id)
        {
            return _collection.FindOneById(id);
        }

        public async Task FindByPropertyAsync<TValue>(Expression<Func<TModel, TValue>> propertySelector, TValue value, Func<TModel, Task> subscription)
        {
            using (var cursor = await _collection.FindAsync(
                Builders<TModel>.Filter.Eq<TValue>(propertySelector, value),
                new FindOptions<TModel>()
                {
                    Sort = Builders<TModel>.Sort.Ascending(m => m.Id)
                }).ConfigureAwait(false))
            {
                foreach (var item in cursor.ToEnumerable())
                {
                    await subscription(item).ConfigureAwait(false);
                }
            }
        }

        public IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter)
        {
            return _collection.AsQueryable().Where(filter).OrderBy(x => x.Id);
        }

        public Task<bool> ContainsAsync(Expression<Func<TModel, bool>> filter)
        {
            return _collection.AsQueryable().AnyAsync(filter);
        }

        public async Task<InsertResult> InsertAsync(TModel model)
        {
            await _collection.InsertOneAsync(model).ConfigureAwait(false);

            return new InsertResult()
            {
                Ok = true,
                ErrorMessage = ""
            };
        }

        public async Task<SaveResult> SaveWithVersionAsync(TModel model, int orignalVersion)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var result = await _collection.FindOneAndReplaceAsync(
                Builders<TModel>.Filter.And(
                    Builders<TModel>.Filter.Eq(x => x.Id, model.Id),
                    Builders<TModel>.Filter.Eq(x => x.Version, orignalVersion)
                    ),
                model,
                new FindOneAndReplaceOptions<TModel, TModel>()
                {
                    Sort = Builders<TModel>.Sort.Ascending(s => s.Id)
                }).ConfigureAwait(false);

            if (result == null)
            {
                //something bad happended, we believe that we have readmodel with that id and original version but it does not
                //seems to be so
                var reloaded = await _collection.FindOneByIdAsync(model.Id);
                Logger.ErrorFormat("Unable to save model {0} with id {1} and original version {2} with new version {3}. In database we found a record with actual version {4} that is not equal to original version!",
                    model.GetType().FullName,
                    model.Id,
                    orignalVersion,
                    model.Version,
                    reloaded?.Version);
                //original code throws because we do not have a way to recover from this, we just added the log.
                throw new JarvisFrameworkEngineException($"Unable to save model {model.GetType().FullName} with id {model.Id} and original version {orignalVersion} with new version {model.Version}. In database we found a record with actual version {reloaded?.Version} that is not equal to original version!");
            }

            return new SaveResult()
            {
                Ok = result.Id != null
            };
        }

        public async Task<DeleteResult> DeleteAsync(TKey id)
        {
            var result = await _collection.RemoveByIdAsync(id).ConfigureAwait(false);

            return new DeleteResult()
            {
                Ok = result != null,
                DocumentsAffected = result.DeletedCount
            };
        }

        public async Task DropAsync()
        {
            await _collection.DropAsync().ConfigureAwait(false);
        }

        public IMongoCollection<TModel> Collection
        {
            get { return _collection; }
        }

        public Task FlushAsync()
        {
            return Task.CompletedTask;
        }
    }
}
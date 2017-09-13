using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Bson;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Driver.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class MongoStorage<TModel, TKey> : IMongoStorage<TModel, TKey> where TModel : class, IReadModelEx<TKey>
    {
        private readonly IMongoCollection<TModel> _collection;

        public MongoStorage(IMongoCollection<TModel> collection)
        {
            _collection = collection;
        }

        public async Task<Boolean> IndexExistsAsync(string indexName)
        {
            var cursor = await _collection.Indexes.ListAsync().ConfigureAwait(false);
            var list = await cursor.ToListAsync().ConfigureAwait(false);
            return list.Any(i => i["name"].AsString == indexName);
        }

        public async Task CreateIndexAsync(String name, IndexKeysDefinition<TModel> keys, CreateIndexOptions options = null)
        {
            options = options ?? new CreateIndexOptions();
            options.Name = name;

            //there is the possibility that create index trow if an index with same name and 
            //different options/keys is created
            try
            {
                await _collection.Indexes.CreateOneAsync(keys, options).ConfigureAwait(false);
            }
            catch (MongoCommandException)
            {
                //probably index exists with different options, drop and recreate
                String indexName = name;
                await _collection.Indexes.DropOneAsync(indexName).ConfigureAwait(false);
                await _collection.Indexes.CreateOneAsync(keys, options).ConfigureAwait(false);
            }
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

        public async Task<IEnumerable<TModel>> FindByPropertyAsync<TValue>(Expression<Func<TModel, TValue>> propertySelector, TValue value)
        {
            var cursor = await _collection.FindAsync(
                Builders<TModel>.Filter.Eq<TValue>(propertySelector, value),
                new FindOptions<TModel>()
                {
                    Sort = Builders<TModel>.Sort.Ascending(m => m.Id)
                }).ConfigureAwait(false);
            return cursor.ToEnumerable();
        }

        public IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter)
        {
            return _collection.AsQueryable().Where(filter).OrderBy(x => x.Id);
        }

        public async Task<bool> ContainsAsync(Expression<Func<TModel, bool>> filter)
        {
            return await _collection.AsQueryable().AnyAsync(filter).ConfigureAwait(false);
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
            return TaskHelpers.CompletedTask;
        }
    }
}
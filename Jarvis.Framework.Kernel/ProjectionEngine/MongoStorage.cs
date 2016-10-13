using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Bson;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Driver.Linq;


namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class MongoStorage<TModel, TKey> : IMongoStorage<TModel, TKey> where TModel : class, IReadModelEx<TKey>
    {
        private readonly IMongoCollection<TModel> _collection;

        public MongoStorage(IMongoCollection<TModel> collection)
        {
            _collection = collection;
        }

        public bool IndexExists(string indexName)
        {
            return _collection.Indexes.List().ToList()
                .Any(i => i["name"].AsString == indexName);
        }

        public void CreateIndex(String name, IndexKeysDefinition<TModel> keys, CreateIndexOptions options = null)
        {
            options = options ?? new CreateIndexOptions();
            options.Name = name;

            //there is the possibility that create index trow if an index with same name and 
            //different options/keys is created
            try
            {
                _collection.Indexes.CreateOne(keys, options);
            }
            catch (MongoCommandException)
            {
                //probably index exists with different options, drop and recreate
                String indexName = name;
                _collection.Indexes.DropOne(indexName);
                _collection.Indexes.CreateOne(keys, options);
            }
        }

        public void InsertBatch(IEnumerable<TModel> values)
        {
            _collection.InsertMany(values);
        }

        public IQueryable<TModel> All
        {
            get { return _collection.AsQueryable(); }
        }

        public TModel FindOneById(TKey id)
        {
            return _collection.FindOneById(id);
        }

        public IEnumerable<TModel> FindManyByProperty<TValue>(Expression<Func<TModel, TValue>> propertySelector, TValue value)
        {
            return _collection.Find(
                Builders<TModel>.Filter.Eq<TValue>(propertySelector, value))
                .Sort(Builders<TModel>.Sort.Ascending(m => m.Id))
                .ToEnumerable();
        }

        public IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter)
        {
            return _collection.AsQueryable().Where(filter).OrderBy(x => x.Id);
        }

        public bool Contains(Expression<Func<TModel, bool>> filter)
        {
            return _collection.AsQueryable().Any(filter);
        }

        public InsertResult Insert(TModel model)
        {
            _collection.InsertOne(model);

            return new InsertResult()
            {
                Ok = true,
                ErrorMessage = ""
            };
        }

        public SaveResult SaveWithVersion(TModel model, int orignalVersion)
        {
            var result = _collection.FindOneAndReplace(
                Builders<TModel>.Filter.And(
                    Builders<TModel>.Filter.Eq(x => x.Id, model.Id),
                    Builders<TModel>.Filter.Eq(x => x.Version, orignalVersion)
                    ),
                model,
                new FindOneAndReplaceOptions<TModel, TModel>()
                {
                    Sort = Builders<TModel>.Sort.Ascending(s => s.Id)
                });

            return new SaveResult()
            {
                Ok = result.Id != null
            };
        }

        public DeleteResult Delete(TKey id)
        {
            var result = _collection.RemoveById(id);

            return new DeleteResult()
            {
                Ok = result != null,
                DocumentsAffected = result.DeletedCount
            };
        }

        public void Drop()
        {
            _collection.Drop();
        }

        public IMongoCollection<TModel> Collection
        {
            get { return _collection; }
        }

        public void Flush()
        {

        }

        #region Helpers



        #endregion
    }
}
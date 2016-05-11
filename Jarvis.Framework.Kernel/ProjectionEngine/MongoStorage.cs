using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class MongoStorage<TModel, TKey> : IMongoStorage<TModel, TKey> where TModel : class, IReadModelEx<TKey>
    {
        private readonly MongoCollection<TModel> _collection;

        public MongoStorage(MongoCollection<TModel> collection)
        {
            _collection = collection;
        }

        public bool IndexExists(IMongoIndexKeys keys)
        {
            return _collection.IndexExists(keys);
        }

        public void CreateIndex(IMongoIndexKeys keys, IMongoIndexOptions options = null)
        {
            if (options != null)
            {
                //there is the possibility that create index trow if an index with same name and 
                //different options/keys is created
                try
                {
                    _collection.CreateIndex(keys, options);
                }
                catch (MongoWriteConcernException ex)
                {
                    //probably index extist with different options, lets check if name is specified
                    var optionsDoc = options.ToBsonDocument();
                    String indexName;
                    if (optionsDoc.Names.Contains("name"))
                    {
                        indexName = optionsDoc["name"].AsString;
                    }
                    else
                    {
                        //determine the index name from fields, fragile but works with this version of drivers 
                        indexName = GetIndexName(keys);
                    }
                    _collection.DropIndexByName(indexName);
                    _collection.CreateIndex(keys, options);
                }
            }
            else
            {
                _collection.CreateIndex(keys);
            }
                
        }

        public void InsertBatch(IEnumerable<TModel> values)
        {
            _collection.InsertBatch(values);
        }

        public IQueryable<TModel> All
        {
            get { return _collection.AsQueryable(); }
        }

        public TModel FindOneById(TKey id)
        {
            return _collection.FindOneById(BsonValue.Create(id));
        }

        public IEnumerable<TModel> FindManyByProperty(Expression<Func<TModel, Object>> propertySelector, Object value)
        {
            return _collection.Find(Query<TModel>.EQ(propertySelector, value))
                .SetSortOrder(SortBy<TModel>.Ascending(m => m.Id));
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
            var result = _collection.Insert(model);

            return new InsertResult()
            {
                Ok = result.Ok,
                ErrorMessage = result.ErrorMessage
            };
        }

        public SaveResult SaveWithVersion(TModel model, int orignalVersion)
        {
            var result = _collection.FindAndModify(new FindAndModifyArgs()
            {
                Query = Query.And(
                    Query<TModel>.EQ(x => x.Id, model.Id),
                    Query<TModel>.EQ(x => x.Version, orignalVersion)
                    ),
                SortBy = SortBy<TModel>.Ascending(s => s.Id),
                Update = Update<TModel>.Replace(model)
            });

            return new SaveResult()
            {
                Ok = result.Ok
            };
        }

        public DeleteResult Delete(TKey id)
        {
            var result = _collection.Remove(Query.EQ("_id", BsonValue.Create(id)));

            return new DeleteResult()
            {
                Ok = result.Ok,
                DocumentsAffected = result.DocumentsAffected
            };
        }

        public void Drop()
        {
            _collection.Drop();
        }

        public MongoCollection<TModel> Collection
        {
            get { return _collection; }
        }

        public IEnumerable<BsonDocument> Aggregate(IEnumerable<BsonDocument> operations)
        {
            return Aggregate(new AggregateArgs()
            {
                Pipeline = operations
            });
        }

        public IEnumerable<BsonDocument> Aggregate(AggregateArgs aggregateArgs)
        {
            return _collection.Aggregate(aggregateArgs);
        }

        public void Flush()
        {

        }

        #region Helpers

        private String GetIndexName(IMongoIndexKeys keys)
        {
            var keysdocument = keys.ToBsonDocument();
            return keysdocument
                .Select(k => k.Name + "_" + k.Value.ToString())
                .Aggregate((s1, s2) => s1 + "_" + s2);
        }

        #endregion
    }
}
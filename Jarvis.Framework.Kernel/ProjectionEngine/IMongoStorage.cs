using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Bson;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface IMongoStorage<TModel, TKey> where TModel : class, IReadModelEx<TKey>
    {
        bool IndexExists(IMongoIndexKeys keys);
        void CreateIndex(IMongoIndexKeys keys, IMongoIndexOptions options = null);
        void InsertBatch(IEnumerable<TModel> values);
        IQueryable<TModel> All { get; }
        TModel FindOneById(TKey id);

        IEnumerable<TModel> FindManyByProperty(Expression<Func<TModel, Object>> propertySelector, Object value);

        IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter);
        bool Contains(Expression<Func<TModel, bool>> filter);
        InsertResult Insert(TModel model);
        SaveResult SaveWithVersion(TModel model, int orignalVersion);
        DeleteResult Delete(TKey id);
        void Drop();
        MongoCollection<TModel> Collection { get; }
        IEnumerable<BsonDocument> Aggregate(IEnumerable<BsonDocument> operations);
        IEnumerable<BsonDocument> Aggregate(AggregateArgs aggregateArgs);
        void Flush();
    }
}
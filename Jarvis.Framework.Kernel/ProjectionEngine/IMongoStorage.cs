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
        bool IndexExists(String name);
        void CreateIndex(String name, IndexKeysDefinition<TModel> keys,  CreateIndexOptions options = null);
        void InsertBatch(IEnumerable<TModel> values);
        IQueryable<TModel> All { get; }
        TModel FindOneById(TKey id);

        IEnumerable<TModel> FindManyByProperty<Tvalue>(Expression<Func<TModel, Tvalue>> propertySelector, Tvalue value);

        IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter);
        bool Contains(Expression<Func<TModel, bool>> filter);
        InsertResult Insert(TModel model);
        SaveResult SaveWithVersion(TModel model, int orignalVersion);
        DeleteResult Delete(TKey id);
        void Drop();
        IMongoCollection<TModel> Collection { get; }

        void Flush();
    }
}
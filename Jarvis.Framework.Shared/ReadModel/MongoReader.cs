﻿using Jarvis.Framework.Shared.Helpers;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel
{
    public class MongoReader<TModel, TKey> : IMongoDbReader<TModel, TKey> where TModel : AbstractReadModel<TKey>
    {
        private readonly IMongoDatabase _readmodelDb;
        private IMongoCollection<TModel> _collection;

        public MongoReader(IMongoDatabase readmodelDb)
        {
            _readmodelDb = readmodelDb;
            CollectionName = CollectionNames.GetCollectionName<TModel>();
        }

        public string CollectionName { get; private set; }

        public virtual IQueryable<TModel> AllUnsorted
        {
            get { return Collection.AsQueryable(); }
        }

        public virtual IQueryable<TModel> AllSortedById
        {
            get { return Collection.AsQueryable().OrderBy(x => x.Id); }
        }

        public virtual Task<TModel> FindOneByIdAsync(TKey id)
        {
            return Collection.FindOneByIdAsync(id);
        }

        public virtual TModel FindOneById(TKey id)
        {
            return Collection.FindOneById(id);
        }

        public virtual IMongoCollection<TModel> Collection
        {
            get { return _collection ?? (_collection = _readmodelDb.GetCollection<TModel>(CollectionName)); }
        }

        public virtual IMongoQueryable<TModel> MongoQueryable
        {
            get { return Collection.AsQueryable(); }
        }
    }
}
﻿using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class MongoReaderForProjections<TModel, TKey> : IMongoDbReader<TModel, TKey> where TModel : AbstractReadModel<TKey>
    {
        private readonly IMongoStorage<TModel, TKey> _storage;

        public MongoReaderForProjections(IMongoStorageFactory factory)
        {
            _storage = factory.GetCollection<TModel, TKey>();
        }

        public IQueryable<TModel> AllUnsorted
        {
            get { return _storage.All; }
        }

        public IQueryable<TModel> AllSortedById
        {
            get { return _storage.All.OrderBy(x => x.Id); }
        }

        public Task<TModel> FindOneByIdAsync(TKey id)
        {
            return _storage.FindOneByIdAsync(id);
        }

        public TModel FindOneById(TKey id)
        {
            return _storage.FindOneById(id);
        }

        public IMongoCollection<TModel> Collection
        {
            get { return _storage.Collection; }
        }

        public IMongoQueryable<TModel> MongoQueryable
        {
            get { return _storage.Collection.AsQueryable(); }
        }
    }
}

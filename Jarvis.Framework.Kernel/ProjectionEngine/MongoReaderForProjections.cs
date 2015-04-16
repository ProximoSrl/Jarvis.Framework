using System.Collections.Generic;
using System.Linq;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class MongoReaderForProjections<TModel, TKey> : IMongoDbReader<TModel, TKey> where TModel : AbstractReadModel<TKey>
    {
        private readonly IMongoStorage<TModel, TKey> _storage;

        public MongoReaderForProjections(IMongoStorageFactory factory)
        {
            _storage = factory.GetCollection<TModel, TKey>();
        }

        public IQueryable<TModel> AllUnsorted {
            get { return _storage.All; }
        }

        public IQueryable<TModel> AllSortedById
        {
            get { return _storage.All.OrderBy(x=>x.Id); }
        }

        public TModel FindOneById(TKey id)
        {
            return _storage.FindOneById(id);
        }

        public IEnumerable<BsonDocument> Aggregate(IEnumerable<BsonDocument> operations)
        {
            return _storage.Aggregate(operations);
        }

        public IEnumerable<BsonDocument> Aggregate(AggregateArgs aggregateArgs)
        {
            return _storage.Aggregate(aggregateArgs);

        }

        public MongoCollection<TModel> Collection {
            get { return _storage.Collection;  }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Jarvis.Framework.Shared.ReadModel
{
    public class MongoReader<TModel, TKey> : IMongoDbReader<TModel, TKey> where TModel : AbstractReadModel<TKey>
    {
        readonly MongoDatabase _readmodelDb;
        MongoCollection<TModel> _collection;

        public MongoReader(MongoDatabase readmodelDb)
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

        public virtual TModel FindOneById(TKey id)
        {
            return Collection.FindOneById(BsonValue.Create(id));
        }

        public virtual IEnumerable<BsonDocument> Aggregate(IEnumerable<BsonDocument> operations)
        {
            return Collection.Aggregate(new AggregateArgs()
            {
                Pipeline = operations,
                OutputMode = AggregateOutputMode.Cursor
            });
//            return Collection.Aggregate(operations).ResultDocuments;
        }

        public IEnumerable<BsonDocument> Aggregate(AggregateArgs aggregateArgs)
        {
            return Collection.Aggregate(aggregateArgs);
        }

        public virtual MongoCollection<TModel> Collection
        {
            get { return _collection ?? (_collection = _readmodelDb.GetCollection<TModel>(CollectionName)); }
        }
    }
}
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IMongoDbReader<TModel, in TKey> : IReader<TModel, TKey> where TModel : AbstractReadModel<TKey>
    {
        IEnumerable<BsonDocument> Aggregate(IEnumerable<BsonDocument> operations);
        IEnumerable<BsonDocument> Aggregate(AggregateArgs aggregateArgs);
        MongoCollection<TModel> Collection { get; }
    }
}
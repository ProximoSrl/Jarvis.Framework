using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IMongoDbReader<TModel, in TKey> : IReader<TModel, TKey> where TModel : AbstractReadModel<TKey>
    {
        IMongoCollection<TModel> Collection { get; }

        IMongoQueryable<TModel> MongoQueryable { get; }
    }
}
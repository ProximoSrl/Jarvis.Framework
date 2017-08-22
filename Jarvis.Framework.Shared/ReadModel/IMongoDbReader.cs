using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IMongoDbReader<TModel, in TKey> : IReader<TModel, TKey> where TModel : AbstractReadModel<TKey>
    {
        IMongoCollection<TModel> Collection { get; }
    }
}

/* Unmerged change from project 'Jarvis.Framework.Shared (net461)'
Before:
using System.Collections.Generic;
using MongoDB.Bson;
After:
using MongoDB.Bson;
using MongoDB.Driver;
*/
using MongoDB.Driver.Linq;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IMongoDbReader<TModel, in TKey> : IReader<TModel, TKey> where TModel : AbstractReadModel<TKey>
    {
        IMongoCollection<TModel> Collection { get; }

        IMongoQueryable<TModel> MongoQueryable { get; }
    }
}
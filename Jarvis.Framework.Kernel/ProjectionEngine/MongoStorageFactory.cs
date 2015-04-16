using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class MongoStorageFactory : IMongoStorageFactory
    {
        readonly MongoDatabase _db;
        readonly IRebuildContext _rebuildContext;

        public MongoStorageFactory(MongoDatabase db, IRebuildContext rebuildContext)
        {
            _db = db;
            _rebuildContext = rebuildContext;
        }

        public IMongoStorage<TModel, TKey> GetCollection<TModel, TKey>() where TModel : class, IReadModelEx<TKey>
        {
            string collectionName = CollectionNames.GetCollectionName<TModel>();
            var cache = _rebuildContext.GetCollection<TModel, TKey>(collectionName);

            return new CachedMongoStorage<TModel, TKey>(
                _db.GetCollection<TModel>(collectionName),
                cache 
            );
        }
    }
}
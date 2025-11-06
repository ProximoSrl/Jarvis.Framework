using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;
using System.Linq;
using System.Threading;
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

        public Task<TModel> FindOneByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            return _storage.FindOneByIdAsync(id, cancellationToken);
        }

        public TModel FindOneById(TKey id)
        {
            return _storage.FindOneById(id);
        }

        public IMongoCollection<TModel> Collection
        {
            get { return _storage.Collection; }
        }

        public IQueryable<TModel> MongoQueryable
        {
            get { return _storage.Collection.AsQueryable(); }
        }

        public IQueryable<TModel> AllUnsortedSecondaryPreferred => _storage.All;
    }
}

using Jarvis.Framework.Shared.ReadModel;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface IMongoStorageFactory
    {
        IMongoStorage<TModel, TKey> GetCollection<TModel, TKey>() where TModel : class, IReadModelEx<TKey>;
    }
}
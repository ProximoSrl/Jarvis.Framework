using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IReader
    {
    }

    /// <summary>
    /// Interface to expose access in readonly mode to a readmodel collection.
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    public interface IReader<TModel, in TKey> : IReader where TModel : AbstractReadModel<TKey>
    {
        /// <summary>
        /// TODO: Exposing everythign as simple IQueryable does not allow to use Mongodb extension
        /// for Async query over LINQ. The interface to expose is an IMongoQueryable, but in that
        /// situation we cannot use the nitro.
        /// </summary>
        IQueryable<TModel> AllUnsorted { get; }

        IQueryable<TModel> AllSortedById { get; }

        /// <summary>
        /// Find one element, and uses the async version to allow for async query in mongo.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
	    Task<TModel> FindOneByIdAsync(TKey id);

        /// <summary>
        /// This is the sync version, needed to avoid having async/await for clients that 
        /// are not ready to move async.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        TModel FindOneById(TKey id);
    }
}
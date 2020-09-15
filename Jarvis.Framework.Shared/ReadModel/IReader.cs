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
        /// Exposing everythign as simple IQueryable does not allow to use Mongodb extension
        /// for Async query over LINQ. We should introduce async interface to IQueryable but it 
        /// is quite complicated, thus, if you need to query async, you should depend from
        /// <see cref="IMongoDbReader{TModel, TKey}"/> and not from standard IReader.
        /// </summary>
        IQueryable<TModel> AllUnsorted { get; }

        /// <summary>
        /// Access to IQueryable already ordered by id.
        /// </summary>
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
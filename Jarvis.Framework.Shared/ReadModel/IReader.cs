using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IReader
    {
    }

    public interface IReader<TModel, in TKey> : IReader where TModel : AbstractReadModel<TKey>
    {
        IQueryable<TModel> AllUnsorted { get; }
        IQueryable<TModel> AllSortedById { get; }

	    Task<TModel> FindOneByIdAsync(TKey id);
    }
}
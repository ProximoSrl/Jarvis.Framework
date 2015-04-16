using System.Linq;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IReader
    {
    }

    public interface IReader<out TModel, in TKey> : IReader where TModel : AbstractReadModel<TKey>
    {
        IQueryable<TModel> AllUnsorted { get; }
        IQueryable<TModel> AllSortedById { get; }
	    TModel FindOneById(TKey id);
    }
}
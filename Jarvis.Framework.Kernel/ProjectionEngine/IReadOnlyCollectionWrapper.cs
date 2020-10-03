using System;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.ReadModel;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface IReadOnlyCollectionWrapper<TModel, in TKey> where TModel : IReadModelEx<TKey>
    {
        IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter);
        IQueryable<TModel> All { get; }

        Task<TModel> FindOneByIdAsync(TKey id);

        Task<Boolean> ContainsAsync(Expression<Func<TModel, bool>> filter);
    }
}
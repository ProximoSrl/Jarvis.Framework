using Jarvis.Framework.Shared.ReadModel;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface IReadOnlyCollectionWrapper<TModel, in TKey> where TModel : IReadModelEx<TKey>
    {
        IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter);
        IQueryable<TModel> All { get; }

        Task<TModel> FindOneByIdAsync(TKey id, CancellationToken cancellationToken = default);

        Task<Boolean> ContainsAsync(Expression<Func<TModel, bool>> filter, CancellationToken cancellationToken = default);
    }
}
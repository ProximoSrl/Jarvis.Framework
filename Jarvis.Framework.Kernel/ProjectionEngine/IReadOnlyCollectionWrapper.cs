using System;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.ReadModel;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface IReadOnlyCollectionWrapper<TModel, in TKey> where TModel : IReadModelEx<TKey>
    {
        IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter);
        IQueryable<TModel> All { get; }
        TModel FindOneById(TKey id);
        bool Contains(Expression<Func<TModel, bool>> filter);
    }
}
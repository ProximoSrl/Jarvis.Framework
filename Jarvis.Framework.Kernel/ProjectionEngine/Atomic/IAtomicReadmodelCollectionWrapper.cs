using Jarvis.Framework.Shared.ReadModel.Atomic;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    /// <summary>
    /// Simple wrapper for readmodel atomic collection. There is no need
    /// for corresponding reader, this is the only interface that will
    /// be used to access atomic readmodels, that are inherited by <see cref="AbstractAtomicReadModel{TKey}"/>
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    public interface IAtomicCollectionReader<TModel>
         where TModel : IAtomicReadModel
    {
        IQueryable<TModel> AsQueryable();

        Task<TModel> FindOneByIdAsync(String id);

        Task<IEnumerable<TModel>> FindManyAsync(System.Linq.Expressions.Expression<Func<TModel, bool>> filter, bool fixVersion = false);
    }

    public interface IAtomicCollectionWrapperFactory
    {
        IAtomicCollectionWrapper<TModel> CreateCollectionWrappper<TModel>()
             where TModel : IAtomicReadModel;
    }

    public interface IAtomicCollectionWrapper<TModel> :
        IAtomicCollectionReader<TModel>
        where TModel : IAtomicReadModel
    {
        /// <summary>
        /// Insert or update a readmodel instance, perform check for idempotency and
        /// readmodel versioning.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task UpsertAsync(TModel model);

        /// <summary>
        /// <para>
        /// Update a readmodel instance into underling storage, it perform
        /// it will check for idempotency, if the object was already saved
        /// and it is newer it will not save again. 
        /// </para>
        /// </summary>
        /// <param name="model"></param>
        /// <remarks>If the readmodel is not present on database, nothing will be written.</remarks>
        /// <returns></returns>
        Task UpdateAsync(TModel model);
    }

    public interface IAtomicMongoCollectionWrapper<TModel> :
       IAtomicCollectionWrapper<TModel>
       where TModel : IAtomicReadModel
    {
        IMongoCollection<TModel> Collection { get; }

        IMongoQueryable AsMongoQueryable();
    }
}

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
        /// <summary>
        /// Allow standard IQueryable interface
        /// </summary>
        /// <returns></returns>
        IQueryable<TModel> AsQueryable();

        /// <summary>
        /// Find a readmodel by id and does some checking like automatic rebuild
        /// of superseded readmodel, etc.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<TModel> FindOneByIdAsync(String id);

        /// <summary>
        /// This is similar to <see cref="FindOneByIdAsync(string)"/> but it will re-query event stream
        /// to project extra events. This ensure that you got the must up-to-date readmodel.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<TModel> FindOneByIdAndCatchupAsync(string id);

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
        /// Insert or update a readmodel instance, avoid any AggregateVersion check, it will
        /// overwrite the value on the database even if the AggregateVersion of <paramref name="model"/> is
        /// lower than the version on disk
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        /// <remarks>Readmodel global signature check is always performed.</remarks>
        Task UpsertForceAsync(TModel model);

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

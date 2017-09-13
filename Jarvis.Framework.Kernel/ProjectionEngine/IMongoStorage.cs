using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Bson;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface IMongoStorage<TModel, TKey> where TModel : class, IReadModelEx<TKey>
    {
        Task<Boolean> IndexExistsAsync(String name);

        Task CreateIndexAsync(String name, IndexKeysDefinition<TModel> keys,  CreateIndexOptions options = null);

        Task InsertBatchAsync(IEnumerable<TModel> values);

        IQueryable<TModel> All { get; }

        /// <summary>
        /// Get a single instance of an element.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<TModel> FindOneByIdAsync(TKey id);

        /// <summary>
        /// Sync version of <see cref="FindOneByIdAsync(TKey)"/>
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        TModel FindOneById(TKey id);

        /// <summary>
        /// Find all the element that have a specific property with a value equal to <paramref name="value"/> 
        /// parameter. This specific property is optimized when inMemoryCollection is active to use in memory
        /// indexes dictionaries.
        /// </summary>
        /// <typeparam name="Tvalue"></typeparam>
        /// <param name="propertySelector"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<IEnumerable<TModel>> FindByPropertyAsync<Tvalue>(Expression<Func<TModel, Tvalue>> propertySelector, Tvalue value);

        IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter);

        Task<bool> ContainsAsync(Expression<Func<TModel, bool>> filter);

        Task<InsertResult> InsertAsync(TModel model);
        Task<SaveResult> SaveWithVersionAsync(TModel model, int orignalVersion);
        Task<DeleteResult> DeleteAsync(TKey id);
        Task DropAsync();

        IMongoCollection<TModel>  Collection { get; }

        Task FlushAsync();
    }
}
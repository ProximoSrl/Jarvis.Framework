using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface ICollectionWrapper<TModel, in TKey> : IReadOnlyCollectionWrapper<TModel, TKey>
        where TModel : IReadModelEx<TKey>
    {
        /// <summary>
        /// Action that will be called each time that a new readmodel is saved. It can 
        /// be left null if you do not need to intercept anything.
        /// </summary>
        Action<TModel, DomainEvent> OnSave { get; set; }

        /// <summary>
        /// If I want to notify a completely different object I can use this transformation
        /// to completely change the readmodel to a different object for transformation.
        /// The transformer function can also return null if the notification should be
        /// sent without payload.
        /// </summary>
        Func<TModel, Object> TransformForNotification { get; set; }

        /// <summary>
        /// Insert a value into the underling storage
        /// </summary>
        /// <param name="e"></param>
        /// <param name="model"></param>
        /// <param name="notify"></param>
        /// <returns></returns>
        Task InsertAsync(DomainEvent e, TModel model, bool notify = false);

        /// <summary>
        /// Upsert a readmodel into the underling persistence storage.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="id"></param>
        /// <param name="insert"></param>
        /// <param name="update"></param>
        /// <param name="notify"></param>
        /// <returns></returns>
        Task<TModel> UpsertAsync(DomainEvent e, TKey id, Func<TModel> insert, Action<TModel> update, bool notify = false);

        /// <summary>
        /// Find a single readmodel and execute an action to modify.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="filter"></param>
        /// <param name="action"></param>
        /// <param name="notify"></param>
        /// <returns></returns>
        Task FindAndModifyAsync(DomainEvent e, Expression<Func<TModel, bool>> filter, Action<TModel> action, bool notify = false);

        /// <summary>
        /// Find a single readmodel and execute an action to modify.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="id"></param>
        /// <param name="action"></param>
        /// <param name="notify"></param>
        /// <returns></returns>
        Task FindAndModifyAsync(DomainEvent e, TKey id, Action<TModel> action, bool notify = false);

        /// <summary>
        /// Find a single readmodel and execute an action to modify, this allow for an action that is 
        /// awaitable, because it return Task.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="id"></param>
        /// <param name="action"></param>
        /// <param name="notify"></param>
        /// <returns></returns>
        Task FindAndModifyAsync(DomainEvent e, TKey id, Func<TModel, Task> action, bool notify = false);

        /// <summary>
        /// Optimize the "in memory" collection allowing to search for a given object that has
        /// a specific property equal to specific value.
        /// 
        /// PAY attention, the items are returned without any given sort.
        /// </summary>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="e"></param>
        /// <param name="propertySelector">A lambda expression used to select property we want to use as filter</param>
        /// <param name="propertyValue">Value of the property.</param>
        /// <param name="action"></param>
        /// <param name="notify"></param>
        Task FindAndModifyByPropertyAsync<TProperty>(DomainEvent e, Expression<Func<TModel, TProperty>> propertySelector, TProperty propertyValue, Action<TModel> action, bool notify = false);

        /// <summary>
        /// Optimize the "in memory" collection allowing to search for a given object that has
        /// a specific property equal to specific value.
        /// 
        /// PAY attention, the items are returned without any given sort.
        /// </summary>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="propertySelector"></param>
        /// <param name="propertyValue"></param>
        Task<IEnumerable<TModel>> FindByPropertyAsync<TProperty>(Expression<Func<TModel, TProperty>> propertySelector, TProperty propertyValue);

        Task SaveAsync(DomainEvent e, TModel model, bool notify = false);

        Task DeleteAsync(DomainEvent e, TKey id, bool notify = false);

        Task DropAsync();

        Task CreateIndexAsync(String name, IndexKeysDefinition<TModel> keys, CreateIndexOptions options = null);

        Task<bool> IndexExistsAsync(String name);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="bEnableNotifications"></param>
        void Attach(IProjection projection, bool bEnableNotifications);

        /// <summary>
        /// Insert a batch of readmodels with a single call.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        Task InsertBatchAsync(IEnumerable<TModel> values);
    }
}
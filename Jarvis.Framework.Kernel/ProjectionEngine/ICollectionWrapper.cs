using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface ICollectionWrapper<TModel, in TKey> : IReadOnlyCollectionWrapper<TModel, TKey>
        where TModel : IReadModelEx<TKey>
    {
        Action<TModel, DomainEvent> OnSave { get; set; }
        Func<TModel, TModel> PrepareForNotification { get; set; }

        void Insert(DomainEvent e, TModel model, bool notify = false);
        TModel Upsert(DomainEvent e, TKey id, Func<TModel> insert, Action<TModel> update, bool notify = false);
        void FindAndModify(DomainEvent e, Expression<Func<TModel, bool>> filter, Action<TModel> action, bool notify = false);
        void FindAndModify(DomainEvent e, TKey id, Action<TModel> action, bool notify = false);

        /// <summary>
        /// Optimize the "in memory" collection allowing to search for a given object that has
        /// a specific property equal to specific value.
        /// 
        /// PAY attention, the items are returned without any given sort.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="propertyToSearch"></param>
        /// <param name="propertySelector"></param>
        /// <param name="action"></param>
        /// <param name="notify"></param>
        void FindAndModifyByProperty(DomainEvent e, Expression<Func<TModel, Object>> propertySelector, Object propertyValue, Action<TModel> action, bool notify = false);

        /// <summary>
        /// Optimize the "in memory" collection allowing to search for a given object that has
        /// a specific property equal to specific value.
        /// 
        /// PAY attention, the items are returned without any given sort.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="propertyToSearch"></param>
        /// <param name="propertySelector"></param>
        /// <param name="action"></param>
        /// <param name="notify"></param>
        IEnumerable<TModel> FindByProperty(Expression<Func<TModel, Object>> propertySelector, Object propertyValue);


        void Save(DomainEvent e, TModel model, bool notify = false);

        void Delete(DomainEvent e, TKey id, bool notify = false);
        void Drop();
        void CreateIndex(String name, IndexKeysDefinition<TModel> keys, CreateIndexOptions options = null);
        bool IndexExists(String name);
        void Attach(IProjection projection,  bool bEnableNotifications);
        void InsertBatch(IEnumerable<TModel> values);
    }
}
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
        void Save(DomainEvent e, TModel model, bool notify = false);
        //        void Delete(Expression<Func<TModel, bool>> filterById, bool notify = false);
        void Delete(DomainEvent e, TKey id, bool notify = false);
        void Drop();
        void CreateIndex(IndexKeysDefinition<TModel> keys, CreateIndexOptions options = null);
        bool IndexExists(IndexKeysDefinition<TModel> keys);
        void Attach(IProjection projection,  bool bEnableNotifications);
        void InsertBatch(IEnumerable<TModel> values);
    }
}
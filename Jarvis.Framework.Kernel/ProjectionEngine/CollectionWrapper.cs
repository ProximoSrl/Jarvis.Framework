using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class CollectionWrapper<TModel, TKey> :
        IObserveProjection,
        ICollectionWrapper<TModel, TKey> where TModel : class, IReadModelEx<TKey>
    {
        private const string ConcurrencyException = "E1100";
        private readonly INotifyToSubscribers _notifyToSubscribers;
        private Action<TModel, DomainEvent> _onSave = (model, e) => { };
        private Func<TModel, Object> _transformForNotification = (model) => model;
        private readonly IMongoStorage<TModel, TKey> _storage;

        public CollectionWrapper(
            IMongoStorageFactory factory,
            INotifyToSubscribers notifyToSubscribers
            )
        {
            _notifyToSubscribers = notifyToSubscribers;
            _storage = factory.GetCollection<TModel, TKey>();
        }

        /// <summary>
        /// Attach a projection to the collection wrapper.
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="bEnableNotifications"></param>
        public void Attach(IProjection projection, bool bEnableNotifications = true)
        {
            if (this.Projection != null)
                throw new Exception("Collection wrapper already attached to projection");

            this.Projection = projection;
            this.NotifySubscribers = bEnableNotifications;
            projection.Observe(this);
        }

        private IProjection Projection { get; set; }

        #region IReadOnlyCollectionWrapper members

        public TModel FindOneById(TKey id)
        {
            return _storage.FindOneById(id);
        }

        public IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter)
        {
            return _storage.Where(filter);
        }

        public bool Contains(Expression<Func<TModel, bool>> filter)
        {
            return _storage.Contains(filter);
        }

        #endregion

        #region ICollectionWrapper members

        public void CreateIndex(String name, IndexKeysDefinition<TModel> keys, CreateIndexOptions options = null)
        {
            _storage.CreateIndex(name, keys, options);
        }

        public bool IndexExists(String name)
        {
            return _storage.IndexExists(name);
        }

        public void InsertBatch(IEnumerable<TModel> values)
        {
            _storage.InsertBatch(values);
        }

        public IQueryable<TModel> All
        {
            get { return _storage.All; }
        }

        protected bool NotifySubscribers { get; set; }

        public Action<TModel, DomainEvent> OnSave
        {
            get { return _onSave; }
            set { _onSave = value; }
        }

        public void Insert(DomainEvent e, TModel model, bool notify = false)
        {
            OnSave(model, e);
            model.Version = 1;
            model.AddEvent(e.MessageId);
            model.LastModified = e.CommitStamp;
            HandlePollableReadModel(e, model);
            try
            {
                var result = _storage.Insert(model);

                if (!result.Ok)
                {
                    throw new Exception("Error writing on mongodb :" + result.ErrorMessage);
                }
            }
            catch (MongoException ex)
            {
                if (!ex.Message.Contains(ConcurrencyException))
                    throw;

                var saved = _storage.FindOneById((dynamic)model.Id);
                if (saved.BuiltFromEvent(e.MessageId))
                    return;

                throw new Exception("Readmodel created by two different events!");
            }

            if (!IsReplay && (notify || NotifySubscribers))
            {
                Object notificationPayload = null;
                notificationPayload = TransformForNotification(model);
                _notifyToSubscribers.Send(ReadModelUpdatedMessage.Created<TModel, TKey>(model.Id, notificationPayload));
            }

        }

        public TModel Upsert(DomainEvent e, TKey id, Func<TModel> insert, Action<TModel> update, bool notify = false)
        {
            var readModel = _storage.FindOneById(id);
            if (readModel != null)
            {
                if (readModel.BuiltFromEvent(e.MessageId))
                    return readModel;

                update(readModel);
                Save(e, readModel, notify);
            }
            else
            {
                readModel = insert();
                readModel.Id = id;
                Insert(e, readModel, notify);
            }

            return readModel;
        }

        public void Save(DomainEvent e, TModel model, bool notify = false)
        {
            OnSave(model, e);
            var orignalVersion = model.Version;
            model.Version++;
            model.AddEvent(e.MessageId);
            model.LastModified = e.CommitStamp;
            HandlePollableReadModel(e, model);
            var result = _storage.SaveWithVersion(model, orignalVersion);

            if (!result.Ok)
                throw new Exception("Concurency exception");

            if (!IsReplay && (notify || NotifySubscribers))
            {
                Object notificationPayload = null;
                notificationPayload = TransformForNotification(model);
                _notifyToSubscribers.Send(ReadModelUpdatedMessage.Updated<TModel, TKey>(model.Id, notificationPayload));
            }
        }

        public void FindAndModify(DomainEvent e, Expression<Func<TModel, bool>> filter, Action<TModel> action, bool notify = false)
        {
            foreach (var model in _storage.All.Where(filter).OrderBy(x => x.Id))
            {
                if (!model.BuiltFromEvent(e.MessageId))
                {
                    action(model);
                    Save(e, model, notify);
                }
            }
        }

        public void FindAndModifyByProperty<TProperty>(DomainEvent e, Expression<Func<TModel, TProperty>> propertySelector, TProperty propertyValue, Action<TModel> action, bool notify = false)
        {
            foreach (var model in _storage.FindByProperty(propertySelector, propertyValue))
            {
                if (!model.BuiltFromEvent(e.MessageId))
                {
                    action(model);
                    Save(e, model, notify);
                }
            }
        }

        public IEnumerable<TModel> FindByProperty<TProperty>(Expression<Func<TModel, TProperty>> propertySelector, TProperty propertyValue)
        {
            return _storage.FindByProperty(propertySelector, propertyValue);
        }

        public void FindAndModify(DomainEvent e, TKey id, Action<TModel> action, bool notify = false)
        {
            var model = _storage.FindOneById(id);
            if (model != null && !model.BuiltFromEvent(e.MessageId))
            {
                action(model);
                Save(e, model, notify);
            }
        }

        /// <summary>
        /// Elimina un readmodel
        /// ?? gestire un flag di eliminato e memorizzare l'id dell'evento??
        /// </summary>
        /// <param name="e"></param>
        /// <param name="id"></param>
        /// <param name="notify"></param>
        public void Delete(DomainEvent e, TKey id, bool notify = false)
        {
            string[] topics = null;

            if (NotifySubscribers && typeof(ITopicsProvider).IsAssignableFrom(typeof(TModel)))
            {
                var model = _storage.FindOneById(id);
                if (model == null)
                    return;

                topics = ((ITopicsProvider)model).GetTopics().ToArray();
            }

            var result = _storage.Delete(id);
            if (!result.Ok)
                throw new Exception(string.Format("Delete error on {0} :: {1}", typeof(TModel).FullName, id));

            if (result.DocumentsAffected == 1)
            {
                if (!IsReplay && (notify || NotifySubscribers))
                    _notifyToSubscribers.Send(ReadModelUpdatedMessage.Deleted<TModel, TKey>(id, topics));
            }
        }

        public void Drop()
        {
            _storage.Drop();
        }

        private static void HandlePollableReadModel(DomainEvent e, TModel model)
        {
            if (model is IPollableReadModel)
            {
                var pollableReadModel = (IPollableReadModel)model;
                pollableReadModel.CheckpointToken = e.CheckpointToken;
                pollableReadModel.LastUpdatedTicks = DateTime.UtcNow.Ticks;
            }
        }

        #endregion

        bool IsReplay
        {
            get
            {
                ThrowIfNotAttached();
                return Projection.IsRebuilding;
            }
        }

        public Func<TModel, Object> TransformForNotification
        {
            get { return _transformForNotification; }
            set { _transformForNotification = value; }
        }

        public void RebuildStarted()
        {
            ThrowIfNotAttached();
        }

        void ThrowIfNotAttached()
        {
            if (Projection == null)
                throw new Exception(string.Format("Projection not attached to {0}", this.GetType().FullName));
        }

        public void RebuildEnded()
        {
            _storage.Flush();
        }

        
    }
}
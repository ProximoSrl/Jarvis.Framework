using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;

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
        private Int64 _pollableSecondaryIndexCounter;

        public CollectionWrapper(
            IMongoStorageFactory factory,
            INotifyToSubscribers notifyToSubscribers
            )
        {
            _notifyToSubscribers = notifyToSubscribers;
            _storage = factory.GetCollection<TModel, TKey>();
            _pollableSecondaryIndexCounter = DateTime.UtcNow.Ticks;
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

        public Task<TModel> FindOneByIdAsync(TKey id)
        {
            return _storage.FindOneByIdAsync(id);
        }

        public IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter)
        {
            return _storage.Where(filter);
        }

        public Task<bool> ContainsAsync(Expression<Func<TModel, bool>> filter)
        {
            return _storage.ContainsAsync(filter);
        }

        #endregion

        #region ICollectionWrapper members

        public Task CreateIndexAsync(String name, IndexKeysDefinition<TModel> keys, CreateIndexOptions options = null)
        {
            return _storage.CreateIndexAsync(name, keys, options);
        }

        public Task<Boolean> IndexExistsAsync(String name)
        {
            return _storage.IndexExistsAsync(name);
        }

        public Task InsertBatchAsync(IEnumerable<TModel> values)
        {
            return _storage.InsertBatchAsync(values);
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

        public async Task InsertAsync(DomainEvent e, TModel model, bool notify = false)
        {
            OnSave(model, e);
            model.Version = 1;
            model.AddEvent(e.MessageId);
            model.LastModified = e.CommitStamp;
            HandlePollableReadModel(e, model);
            try
            {
                var result = await _storage.InsertAsync(model).ConfigureAwait(false);

                if (!result.Ok)
                {
                    throw new Exception("Error writing on mongodb :" + result.ErrorMessage);
                }
            }
            catch (MongoException ex)
            {
                if (!ex.Message.Contains(ConcurrencyException))
                    throw;

                var saved = await _storage.FindOneByIdAsync((dynamic)model.Id).ConfigureAwait(false);
                if (saved.BuiltFromEvent(e.MessageId))
                    return;

                throw new Exception("Readmodel created by two different events!");
            }

            if (!IsReplay && (notify || NotifySubscribers))
            {
                Object notificationPayload = TransformForNotification(model);
                await _notifyToSubscribers.Send(ReadModelUpdatedMessage.Created<TModel, TKey>(model.Id, notificationPayload)).ConfigureAwait(false);
            }
        }

        public async Task<TModel> UpsertAsync(DomainEvent e, TKey id, Func<TModel> insert, Action<TModel> update, bool notify = false)
        {
            var readModel = await _storage.FindOneByIdAsync(id).ConfigureAwait(false);
            if (readModel != null)
            {
                if (readModel.BuiltFromEvent(e.MessageId))
                    return readModel;

                update(readModel);
                await SaveAsync(e, readModel, notify).ConfigureAwait(false);
            }
            else
            {
                readModel = insert();
                readModel.Id = id;
                await InsertAsync(e, readModel, notify).ConfigureAwait(false);
            }

            return readModel;
        }

        public async Task SaveAsync(DomainEvent e, TModel model, bool notify = false)
        {
            OnSave(model, e);
            var orignalVersion = model.Version;
            model.Version++;
            model.AddEvent(e.MessageId);
            model.LastModified = e.CommitStamp;
            HandlePollableReadModel(e, model);
            var result = await _storage.SaveWithVersionAsync(model, orignalVersion).ConfigureAwait(false);

            if (!result.Ok)
                throw new Exception("Concurency exception");

            if (!IsReplay && (notify || NotifySubscribers))
            {
                Object notificationPayload = TransformForNotification(model);
                await _notifyToSubscribers.Send(ReadModelUpdatedMessage.Updated<TModel, TKey>(model.Id, notificationPayload)).ConfigureAwait(false);
            }
        }

        public async Task FindAndModifyAsync(DomainEvent e, Expression<Func<TModel, bool>> filter, Action<TModel> action, bool notify = false)
        {
            foreach (var model in _storage.All.Where(filter).OrderBy(x => x.Id))
            {
                if (!model.BuiltFromEvent(e.MessageId))
                {
                    action(model);
                    await SaveAsync(e, model, notify).ConfigureAwait(false);
                }
            }
        }
     
        public async Task FindAndModifyByPropertyAsync<TProperty>(DomainEvent e, Expression<Func<TModel, TProperty>> propertySelector, TProperty propertyValue, Action<TModel> action, bool notify = false)
        {
            var result = await _storage.FindByPropertyAsync(propertySelector, propertyValue).ConfigureAwait(false);
            foreach (var model in result)
            {
                if (!model.BuiltFromEvent(e.MessageId))
                {
                    action(model);
                    await SaveAsync(e, model, notify).ConfigureAwait(false);
                }
            }
        }

        public Task<IEnumerable<TModel>> FindByPropertyAsync<TProperty>(Expression<Func<TModel, TProperty>> propertySelector, TProperty propertyValue)
        {
            return _storage.FindByPropertyAsync(propertySelector, propertyValue);
        }

        public Task FindAndModifyAsync(DomainEvent e, TKey id, Action<TModel> action, bool notify = false)
        {
            //Delegates to the async function.
            return FindAndModifyAsync(e, id, m => {
                action(m);
                return TaskHelpers.CompletedTask;
            });
        }

        public async Task FindAndModifyAsync(DomainEvent e, TKey id, Func<TModel, Task> action, bool notify = false)
        {
            var model = await _storage.FindOneByIdAsync(id).ConfigureAwait(false);
            if (model != null && !model.BuiltFromEvent(e.MessageId))
            {
                await action(model).ConfigureAwait(false);
                await SaveAsync(e, model, notify).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Elimina un readmodel
        /// ?? gestire un flag di eliminato e memorizzare l'id dell'evento??
        /// </summary>
        /// <param name="e"></param>
        /// <param name="id"></param>
        /// <param name="notify"></param>
        public async Task DeleteAsync(DomainEvent e, TKey id, bool notify = false)
        {
            string[] topics = null;

            if (NotifySubscribers && typeof(ITopicsProvider).IsAssignableFrom(typeof(TModel)))
            {
                var model = await _storage.FindOneByIdAsync(id).ConfigureAwait(false);
                if (model == null)
                    return;

                topics = ((ITopicsProvider)model).GetTopics().ToArray();
            }

            var result = await _storage.DeleteAsync(id).ConfigureAwait(false);
            if (!result.Ok)
                throw new Exception(string.Format("Delete error on {0} :: {1}", typeof(TModel).FullName, id));

            if (result.DocumentsAffected == 1)
            {
                if (!IsReplay && (notify || NotifySubscribers))
                    await _notifyToSubscribers.Send(ReadModelUpdatedMessage.Deleted<TModel, TKey>(id, topics)).ConfigureAwait(false);
            }
        }

        public Task DropAsync()
        {
            return _storage.DropAsync();
        }

        private void HandlePollableReadModel(DomainEvent e, TModel model)
        {
            if (model is IPollableReadModel)
            {
                var pollableReadModel = (IPollableReadModel)model;
                pollableReadModel.CheckpointToken = e.CheckpointToken;
                pollableReadModel.SecondaryToken = _pollableSecondaryIndexCounter++;
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

        public Task RebuildStartedAsync()
        {
            ThrowIfNotAttached();
            return TaskHelpers.CompletedTask;
        }

        void ThrowIfNotAttached()
        {
            if (Projection == null)
                throw new Exception(string.Format("Projection not attached to {0}", this.GetType().FullName));
        }

        public Task RebuildEndedAsync()
        {
            return _storage.FlushAsync();
        }
    }
}
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

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class CollectionWrapper<TModel, TKey> :
        IObserveProjection,
        ICollectionWrapper<TModel, TKey> where TModel : class, IReadModelEx<TKey>
    {
        private const string ConcurrencyException = "E1100";
        private readonly INotifyToSubscribers _notifyToSubscribers;
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

        private string FormatCollectionWrapperExceptionMessage(string message)
        {
            return $"CollectionWrapper [{typeof(TModel).Name}] - {message}";
        }

        /// <summary>
        /// Attach a projection to the collection wrapper.
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="bEnableNotifications"></param>
        public void Attach(IProjection projection, bool bEnableNotifications = true)
        {
            if (this.Projection != null)
                throw new CollectionWrapperException(FormatCollectionWrapperExceptionMessage("already attached to projection."));

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

        public Action<TModel, DomainEvent> OnSave { get; set; } = (model, e) => { };

        public async Task InsertAsync(DomainEvent e, TModel model, bool notify = false)
        {
            OnSave(model, e);
            model.Version = 1;
            model.LastModified = e.CommitStamp;

            model.AddEvent(e.MessageId);
            HandlePollableReadModel(e, model);
            try
            {
                var result = await _storage.InsertAsync(model).ConfigureAwait(false);

                if (!result.Ok)
                {
                    throw new CollectionWrapperException(FormatCollectionWrapperExceptionMessage("Error writing on mongodb :" + result.ErrorMessage));
                }
            }
            catch (MongoException ex)
            {
                if (!ex.Message.Contains(ConcurrencyException))
                    throw;

                var saved = await _storage.FindOneByIdAsync((dynamic)model.Id).ConfigureAwait(false);
                if (saved.BuiltFromEvent(e))
                    return;

                throw new CollectionWrapperException(FormatCollectionWrapperExceptionMessage("Readmodel created by two different events!"));
            }

            if (!IsReplay && (notify || NotifySubscribers))
            {
                Object notificationPayload = TransformForNotification(model);
                await _notifyToSubscribers.Send(ReadModelUpdatedMessage.Created<TModel, TKey>(model.Id, notificationPayload)).ConfigureAwait(false);
            }
        }

        public async Task<TModel> UpsertAsync(DomainEvent e, TKey id, Func<TModel> insert, Func<TModel, Task> update, bool notify = false)
        {
            var readModel = await _storage.FindOneByIdAsync(id).ConfigureAwait(false);
            if (readModel != null)
            {
                if (readModel.BuiltFromEvent(e))
                    return readModel;

                await update(readModel).ConfigureAwait(false);
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
            model.LastModified = e.CommitStamp;
            model.AddEvent(e.MessageId);
            HandlePollableReadModel(e, model);
            var result = await _storage.SaveWithVersionAsync(model, orignalVersion).ConfigureAwait(false);

            if (!result.Ok)
                throw new CollectionWrapperException(FormatCollectionWrapperExceptionMessage("Concurency exception"));

            if (!IsReplay && (notify || NotifySubscribers))
            {
                Object notificationPayload = TransformForNotification(model);
                await _notifyToSubscribers.Send(ReadModelUpdatedMessage.Updated<TModel, TKey>(model.Id, notificationPayload)).ConfigureAwait(false);
            }
        }

        public async Task FindAndModifyAsync(DomainEvent e, Expression<Func<TModel, bool>> filter, Func<TModel, Task> action, bool notify = false)
        {
            foreach (var model in _storage.All.Where(filter).OrderBy(x => x.Id))
            {
                if (!model.BuiltFromEvent(e))
                {
                    await action(model).ConfigureAwait(false);
                    await SaveAsync(e, model, notify).ConfigureAwait(false);
                }
            }
        }

        public async Task FindAndModifyByPropertyAsync<TProperty>(
            DomainEvent e,
            Expression<Func<TModel, TProperty>> propertySelector,
            TProperty propertyValue,
            Func<TModel, Task> action,
            bool notify = false)
        {
            Func<TModel, Task> saveAction = async model =>
            {
                if (!model.BuiltFromEvent(e))
                {
                    await action(model).ConfigureAwait(false);
                    await SaveAsync(e, model, notify).ConfigureAwait(false);
                }
            };
            await _storage.FindByPropertyAsync(propertySelector, propertyValue, saveAction).ConfigureAwait(false);
        }

        public Task FindByPropertyAsync<TProperty>(Expression<Func<TModel, TProperty>> propertySelector, TProperty propertyValue, Func<TModel, Task> subscription)
        {
            return _storage.FindByPropertyAsync(propertySelector, propertyValue, subscription);
        }

        public async Task FindAndModifyAsync(DomainEvent e, TKey id, Func<TModel, Task> action, bool notify = false)
        {
            var model = await _storage.FindOneByIdAsync(id).ConfigureAwait(false);
            if (model != null && !model.BuiltFromEvent(e))
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
                throw new CollectionWrapperException(FormatCollectionWrapperExceptionMessage(string.Format("Delete error on {0} :: {1}", typeof(TModel).FullName, id)));

            if (result.DocumentsAffected == 1 && !IsReplay && (notify || NotifySubscribers))
            {
                await _notifyToSubscribers.Send(ReadModelUpdatedMessage.Deleted<TModel, TKey>(id, topics)).ConfigureAwait(false);
            }
        }

        public Task DropAsync()
        {
            return _storage.DropAsync();
        }

        private void HandlePollableReadModel(DomainEvent e, TModel model)
        {
            IPollableReadModel pollableReadModel;
            if ((pollableReadModel = model as IPollableReadModel) != null)
            {
                pollableReadModel.CheckpointToken = e.CheckpointToken;
                pollableReadModel.SecondaryToken = _pollableSecondaryIndexCounter++;
            }
        }

        #endregion

        private bool IsReplay
        {
            get
            {
                ThrowIfNotAttached();
                return Projection.IsRebuilding;
            }
        }

        public Func<TModel, Object> TransformForNotification { get; set; } = (model) => model;

        public Task RebuildStartedAsync()
        {
            ThrowIfNotAttached();
            return Task.CompletedTask;
        }

        private void ThrowIfNotAttached()
        {
            if (Projection == null)
                throw new CollectionWrapperException(FormatCollectionWrapperExceptionMessage(string.Format("Projection not attached to {0}", this.GetType().FullName)));
        }

        public Task RebuildEndedAsync()
        {
            return _storage.FlushAsync();
        }
    }
}
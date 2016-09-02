using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.NEventStoreEx.CommonDomainEx;
using NEventStore;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Events;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Unfolder
{
    public class QueryModelRepository : IQueryModelRepository
    {

        private readonly IProjectorFactory _factory;
        private readonly IStoreEvents _eventStore;
        private readonly ISnapshotPersister _snapshotPersister;

        /// <summary>
        /// Sample class to handle load a <see cref="Projector{TQueryModel}"/> given an identity
        /// and a given event value.
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="eventStore"></param>
        /// <param name="snapshotPersister"></param>
        public QueryModelRepository(
            IProjectorFactory factory,
            IStoreEvents eventStore,
            ISnapshotPersister snapshotPersister)
        {
            _factory = factory;
            _eventStore = eventStore;
            _snapshotPersister = snapshotPersister;
        }

        public TProjector GetById<TProjector, TViewModel>(IIdentity id, Int32 to)
            where TProjector : Projector<TViewModel>
            where TViewModel : BaseAggregateQueryModel, new()
        {
            var snapshotTypeName = typeof(TProjector).Name;
            var snapshot = _snapshotPersister.Load(id.AsString(), to, snapshotTypeName);
            var projector = _factory.Create<TViewModel>(typeof(TProjector), id);

            Int32 startsFromEvent = 0;
            IMementoEx memento = snapshot == null ? null : (IMementoEx)snapshot.Payload;
            if (memento != null)
            {
                if (!projector.ValidateMemento(memento))
                {   _snapshotPersister.Clear(id.AsString(), memento.Version, snapshotTypeName);}
                else
                {
                    projector.Restore(memento);
                    startsFromEvent = snapshot.StreamRevision;

                }
            }

            using (var events = _eventStore.OpenStream(projector.BucketId, id.AsString(), startsFromEvent, to))
            {
                foreach (var evt in events.CommittedEvents)
                {
                    projector.ApplyEvent((DomainEvent)evt.Body);
                }

                if (projector.ShouldSnapshot())
                {
                    memento = projector.GetSnapshot();
                    snapshot = new Snapshot(id.AsString(), events.StreamRevision, memento);
                    _snapshotPersister.Persist(snapshot, snapshotTypeName);
                }
            }
           
            return (TProjector) projector;
        }

        Boolean _disposed;

        public void Dispose(Boolean dispose)
        {
            if (_disposed) return;
            if (dispose)
            {
                OnDispose();
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void OnDispose()
        {
            
        }

    }

    public class ProjectorManager :  IProjectorManager
    {
        IQueryModelRepository _repository;

        public ProjectorManager(IQueryModelRepository repository)
        {
            _repository = repository;
        }

        public TQueryModel Project<TUnfolder, TQueryModel>(IIdentity id, int to)
            where TUnfolder : Projector<TQueryModel>
            where TQueryModel : BaseAggregateQueryModel, new()
        {
            var unwinder = _repository.GetById<TUnfolder, TQueryModel>(id, to);
            return unwinder.GetProjection();
        }
    }
}

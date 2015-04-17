using System;
using System.Collections.Generic;
using System.Linq;
using CommonDomain;
using CommonDomain.Persistence;
using NEventStore;
using NEventStore.Persistence;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
    public class AbstractRepository : IRepositoryEx
    {
        private const string AggregateTypeHeader = "AggregateType";

        private readonly IDetectConflicts _conflictDetector;

        private readonly IStoreEvents _eventStore;

        private readonly IConstructAggregatesEx _factory;
        private readonly IIdentityConverter _identityConverter;
        private readonly IDictionary<string, ISnapshot> _snapshots = new Dictionary<string, ISnapshot>();

        private readonly IDictionary<string, IEventStream> _streams = new Dictionary<string, IEventStream>();

        protected AbstractRepository(IStoreEvents eventStore, IConstructAggregatesEx factory, IDetectConflicts conflictDetector, IIdentityConverter identityConverter)
        {
            this._eventStore = eventStore;
            this._factory = factory;
            this._conflictDetector = conflictDetector;
            this._identityConverter = identityConverter;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual TAggregate GetById<TAggregate>(IIdentity id) where TAggregate : class, IAggregateEx
        {
            return this.GetById<TAggregate>(Bucket.Default, id);
        }

        public virtual TAggregate GetById<TAggregate>(IIdentity id, int versionToLoad) where TAggregate : class, IAggregateEx
        {
            return this.GetById<TAggregate>(Bucket.Default, id, versionToLoad);
        }

        public TAggregate GetById<TAggregate>(string bucketId, IIdentity id) where TAggregate : class, IAggregateEx
        {
            return this.GetById<TAggregate>(bucketId, id, int.MaxValue);
        }

        public TAggregate GetById<TAggregate>(string bucketId, IIdentity id, int versionToLoad) where TAggregate : class, IAggregateEx
        {
            ISnapshot snapshot = this.GetSnapshot(bucketId, id, versionToLoad);
            IEventStream stream = this.OpenStream(bucketId, id, versionToLoad, snapshot);
            IAggregateEx aggregate = this.GetAggregate<TAggregate>(snapshot, stream);

            ApplyEventsToAggregate(versionToLoad, stream, aggregate);

            return aggregate as TAggregate;
        }

        public virtual void Save(IAggregateEx aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            Save(Bucket.Default, aggregate, commitId, updateHeaders);

        }

        public void Save(string bucketId, IAggregateEx aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            Dictionary<string, object> headers = PrepareHeaders(aggregate, updateHeaders);
            while (true)
            {
                IEventStream stream = this.PrepareStream(bucketId, aggregate, headers);
                int commitEventCount = stream.CommittedEvents.Count;

                try
                {
                    stream.CommitChanges(commitId);
                    aggregate.ClearUncommittedEvents();
                    return;
                }
                catch (DuplicateCommitException)
                {
                    stream.ClearChanges();
                    return;
                }
                catch (ConcurrencyException e)
                {

                    if (this.ThrowOnConflict(stream, commitEventCount))
                    {
                        //@@FIX -> aggiungere prima del lancio dell'eccezione
                        stream.ClearChanges();
                        throw new ConflictingCommandException(e.Message, e);
                    }
                    stream.ClearChanges();
                }
                catch (StorageException e)
                {
                    throw new PersistenceException(e.Message, e);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            lock (this._streams)
            {
                foreach (var stream in this._streams)
                {
                    stream.Value.Dispose();
                }

                this._snapshots.Clear();
                this._streams.Clear();
            }
        }

        private static void ApplyEventsToAggregate(int versionToLoad, IEventStream stream, IAggregateEx aggregate)
        {
            if (versionToLoad == 0 || aggregate.Version < versionToLoad)
            {
                foreach (var @event in stream.CommittedEvents.Select(x => x.Body))
                {
                    aggregate.ApplyEvent(@event);
                }
            }
        }

        private IAggregateEx GetAggregate<TAggregate>(ISnapshot snapshot, IEventStream stream)
        {
            IMementoEx memento = snapshot == null ? null : snapshot.Payload as IMementoEx;
            return this._factory.Build(typeof(TAggregate), _identityConverter.ToIdentity(stream.StreamId), memento);
        }

        private ISnapshot GetSnapshot(string bucketId, IIdentity id, int version)
        {
            ISnapshot snapshot;
            var snapshotId = bucketId + "@" + id;
            if (!this._snapshots.TryGetValue(snapshotId, out snapshot))
            {
                this._snapshots[snapshotId] = snapshot = this._eventStore.Advanced.GetSnapshot(bucketId, _identityConverter.ToString(id), version);
            }

            return snapshot;
        }

        private IEventStream OpenStream(string bucketId, IIdentity id, int version, ISnapshot snapshot)
        {
            IEventStream stream;
            var streamsId = bucketId + "@" + id;
            if (this._streams.TryGetValue(streamsId, out stream))
            {
                return stream;
            }

            stream = snapshot == null ?
                           this._eventStore.OpenStream(bucketId, id.AsString(), 0, version)
                         : this._eventStore.OpenStream(snapshot, version);

            return this._streams[streamsId] = stream;
        }

        private IEventStream PrepareStream(string bucketId, IAggregateEx aggregate, Dictionary<string, object> headers)
        {
            IEventStream stream;
            //@@FIX
            var streamsId = bucketId + "@" + aggregate.Id;
            if (!this._streams.TryGetValue(streamsId, out stream))
            {
                this._streams[streamsId] = stream = this._eventStore.CreateStream(bucketId, _identityConverter.ToString(aggregate.Id));
            }

            foreach (var item in headers)
            {
                stream.UncommittedHeaders[item.Key] = item.Value;
            }

            aggregate.GetUncommittedEvents()
                     .Cast<object>()
                     .Select(x => new EventMessage { Body = x })
                     .ToList()
                     .ForEach(stream.Add);

            return stream;
        }

        private static Dictionary<string, object> PrepareHeaders(
            IAggregateEx aggregate, Action<IDictionary<string, object>> updateHeaders)
        {
            var headers = new Dictionary<string, object>();

            headers[AggregateTypeHeader] = aggregate.GetType().FullName;
            if (updateHeaders != null)
            {
                updateHeaders(headers);
            }

            return headers;
        }

        private bool ThrowOnConflict(IEventStream stream, int skip)
        {
            IEnumerable<object> committed = stream.CommittedEvents.Skip(skip).Select(x => x.Body);
            IEnumerable<object> uncommitted = stream.UncommittedEvents.Select(x => x.Body);
            return this._conflictDetector.ConflictsWith(uncommitted, committed);
        }
    }
}
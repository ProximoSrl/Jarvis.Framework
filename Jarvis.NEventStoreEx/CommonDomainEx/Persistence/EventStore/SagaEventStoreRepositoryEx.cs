using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using CommonDomain.Persistence;
using NEventStore;
using NEventStore.Persistence;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
    public class SagaEventStoreRepositoryEx : ISagaRepositoryEx, IDisposable
    {
        const string SagaTypeHeader = "SagaType";

        const string UndispatchedMessageHeader = "UndispatchedMessage.";

        readonly IStoreEvents _eventStore;

        readonly IDictionary<string, IEventStream> _streams = new Dictionary<string, IEventStream>();
        readonly ISagaFactory _factory;

        /// <summary>
        /// ConcurrentBag has no simple way to remove specific element.
        /// </summary>
        protected static ConcurrentDictionary<String, Boolean> idSerializerDictionary;

        private readonly HashSet<String> loadedSagaIdentities = new HashSet<String>();

        static SagaEventStoreRepositoryEx()
        {
            idSerializerDictionary = new ConcurrentDictionary<String, Boolean>();
        }


        public SagaEventStoreRepositoryEx(IStoreEvents eventStore, ISagaFactory factory)
        {
            _eventStore = eventStore;
            _factory = factory;
        }

        public TSaga GetById<TSaga>(string sagaId) where TSaga : class, ISagaEx
        {
            //try to acquire a lock on the identity, to minimize risk of ConcurrencyException
            //do not sleep more than a certain amount of time (avoid some missing dispose).
            Int32 sleepCount = 0;
            while (!idSerializerDictionary.TryAdd(sagaId, true) && sleepCount < 100)
            {
                //some other thread is accessing that entity. Sleeping is the best choiche, because
                //the lock will be removed after a save, involving IO.
                Thread.Sleep(50);
                sleepCount++;
            }
            return BuildSaga<TSaga>(OpenStream(sagaId), sagaId);
        }

        public void Save(ISagaEx saga, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            if (saga == null)
            {
                throw new ArgumentNullException("saga");
            }

            Dictionary<string, object> headers = PrepareHeaders(saga, updateHeaders);
            IEventStream stream = PrepareStream(saga, headers);

            Persist(stream, commitId);
            ReleaseAggregateId(saga.Id);
            saga.ClearUncommittedEvents();
            saga.ClearUndispatchedMessages();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            foreach (var id in loadedSagaIdentities)
            {
                ReleaseAggregateId(id);
            }
            loadedSagaIdentities.Clear();

            lock (_streams)
            {
                foreach (var stream in _streams)
                {
                    stream.Value.Dispose();
                }

                _streams.Clear();
            }
        }

        private void ReleaseAggregateId(String id)
        {
            Boolean outTempValue;

            if (!idSerializerDictionary.TryRemove(id, out outTempValue))
            {
                Debug.WriteLine("Entity was not locked.");
            }
        }

        IEventStream OpenStream(string sagaId)
        {
            IEventStream stream;
            if (_streams.TryGetValue(sagaId, out stream))
            {
                return stream;
            }

            try
            {
                stream = _eventStore.OpenStream(sagaId, 0, int.MaxValue);
            }
            catch (StreamNotFoundException)
            {
                stream = _eventStore.CreateStream(sagaId);
            }

            return _streams[sagaId] = stream;
        }

        private TSaga BuildSaga<TSaga>(IEventStream stream, String id) where TSaga : class, ISagaEx
        {
            var saga = (TSaga)_factory.Build(typeof(TSaga), id);
            saga.SetReplayMode(true);
            foreach (Object @event in stream.CommittedEvents.Select(x => x.Body))
            {
                saga.Transition(@event);
            }

            saga.ClearUncommittedEvents();
            saga.ClearUndispatchedMessages();
            saga.SetReplayMode(false);

            return saga;
        }

        static Dictionary<string, object> PrepareHeaders(
            ISagaEx saga, Action<IDictionary<string, object>> updateHeaders)
        {
            var headers = new Dictionary<string, object>();

            headers[SagaTypeHeader] = saga.GetType().FullName;
            if (updateHeaders != null)
            {
                updateHeaders(headers);
            }

            int i = 0;
            foreach (object command in saga.GetUndispatchedMessages())
            {
                headers[UndispatchedMessageHeader + i++] = command;
            }

            return headers;
        }

        IEventStream PrepareStream(ISagaEx saga, Dictionary<string, object> headers)
        {
            IEventStream stream;
            if (!_streams.TryGetValue(saga.Id, out stream))
            {
                _streams[saga.Id] = stream = _eventStore.CreateStream(saga.Id);
            }

            foreach (var item in headers)
            {
                stream.UncommittedHeaders[item.Key] = item.Value;
            }

            Enumerable.Cast<object>(saga.GetUncommittedEvents())
                .Select(x => new EventMessage {Body = x})
                .ToList()
                .ForEach(stream.Add);

            return stream;
        }

        static void Persist(IEventStream stream, Guid commitId)
        {
            try
            {
                stream.CommitChanges(commitId);
            }
            catch (DuplicateCommitException)
            {
                stream.ClearChanges();
            }
            catch (StorageException e)
            {
                throw new PersistenceException(e.Message, e);
            }
        }

       
    }
}
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Events;
using MongoDB.Driver;
using NEventStore;
using NEventStore.Persistence;
using NEventStore.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    /// <summary>
    /// This is a class used to read all the events from NES and then unwind
    /// all events in a separate collection that will be used during rebuild
    /// </summary>
    public class EventUnwinder
    {
        IStoreEvents _eventStore;
        IPersistStreams _persistStream;
        IMongoDatabase _mongoDatabase;
        IMongoCollection<UnwindedDomainEvent> _unwindedEventCollection;

        ILogger _logger;

        readonly ProjectionEngineConfig _config;

        public EventUnwinder(
            ProjectionEngineConfig config,
            ILogger logger)
        {
            _config = config;
            _logger = logger;
            _eventStore = Wireup
                .Init()
                //.LogTo(t => new NEventStoreLog4NetLogger(_logger))
                .UsingMongoPersistence(() => _config.EventStoreConnectionString, new DocumentObjectSerializer())
                .InitializeStorageEngine()
                .Build();

            var url = new MongoUrl(_config.EventStoreConnectionString);
            var client = new MongoClient(url);
            _mongoDatabase = client.GetDatabase(url.DatabaseName);

            _persistStream = _eventStore.Advanced;

            _unwindedEventCollection = _mongoDatabase.GetCollection<UnwindedDomainEvent>("UnwindedEvents");
            _unwindedEventCollection.Indexes.CreateOne(
                Builders<UnwindedDomainEvent>.IndexKeys
                    .Ascending(ude => ude.CheckpointToken)
                    .Ascending(ude => ude.EventSequence)
                    .Ascending(ude => ude.EventType),
                new CreateIndexOptions()
                {
                    Name = "ScanPrimary"
                }
            );
        }

        public IMongoCollection<UnwindedDomainEvent> UnwindedCollection
        {
            get { return _unwindedEventCollection; }
        }

        public void Unwind()
        {
            var lastEvent = _unwindedEventCollection
                .Find(Builders<UnwindedDomainEvent>.Filter.Empty)
                .Sort(Builders<UnwindedDomainEvent>.Sort.Descending(e => e.CheckpointToken))
                .Limit(1)
                .SingleOrDefault();
            Int64 startToken = 0;
            if (lastEvent != null)
            {
                startToken = lastEvent.CheckpointToken;
            }

            _logger.InfoFormat("Unwind events starting from commit {0}", startToken);

            //Since we could have crashed during unwind of last commit, we want to reunwind again last commit
            Int64 checkpointToken = startToken - 1;
            _unwindedEventCollection.DeleteMany(Builders<UnwindedDomainEvent>.Filter.Gte(e => e.CheckpointToken, startToken));

            Int64 count = 0;
            List<UnwindedDomainEvent> batchEventUnwind = new List<UnwindedDomainEvent>(550);
            foreach (var commit in _persistStream.GetFrom(checkpointToken))
            {
                foreach (var evt in UnwindCommit(commit))
                {
                    batchEventUnwind.Add(evt);
                    if (batchEventUnwind.Count > 500)
                    {
                        _unwindedEventCollection.InsertMany(batchEventUnwind);
                        batchEventUnwind.Clear();
                    }
                }
                checkpointToken = commit.CheckpointToken;
                if (++count % 10000 == 0)
                {
                    _logger.InfoFormat("Unwinded block of 10000 commit, now we are at checkpoint {0}", checkpointToken);
                }
            }
            if (batchEventUnwind.Count > 0)
            {
                _unwindedEventCollection.InsertMany(batchEventUnwind);
            }

            _logger.InfoFormat("Unwind events ends, started from commit {0} and ended with commit {1}", startToken, checkpointToken);
        }

        private IEnumerable<UnwindedDomainEvent> UnwindCommit(ICommit commit)
        {
            Int32 ordinal = 1;
            foreach (var evt in commit.Events)
            {
                var id = commit.CheckpointToken + "_" + ordinal;
                UnwindedDomainEvent unwinded = new UnwindedDomainEvent();
                unwinded.Id = id;
                unwinded.CheckpointToken = commit.CheckpointToken;
                unwinded.EventSequence = ordinal;
                unwinded.EventType = evt.Body.GetType().Name;
                unwinded.Event = (DomainEvent)evt.Body;

                //save all data from commit to the event unwinded to do enhance after load.
                var headers = commit.Headers;
                if (evt.Headers.Count > 0)
                {
                    headers = commit.Headers.ToDictionary(k => k.Key, k => k.Value);
                    foreach (var eventHeader in evt.Headers)
                    {
                        headers[eventHeader.Key] = eventHeader.Value;
                    }
                }
                unwinded.CommitStamp = commit.CommitStamp;
                unwinded.CommitId = commit.CommitId;
                unwinded.Version = commit.StreamRevision;
                unwinded.Context = headers;

                yield return unwinded;
                ordinal++;
            }
        }

    }
}

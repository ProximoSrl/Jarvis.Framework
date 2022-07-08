using Castle.Core.Logging;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Store;
using Jarvis.Framework.Shared.Support;
using MongoDB.Driver;
using NStore.Core.Persistence;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    /// <summary>
    /// This is a class used to read all the events from NES and then unwind
    /// all events in a separate collection that will be used during rebuild
    /// </summary>
    public class EventUnwinder
    {
        private readonly IPersistence _persistence;
        private readonly IMongoCollection<UnwindedDomainEvent> _unwindedEventCollection;
        private readonly ILogger _logger;

        private readonly ProjectionEngineConfig _config;

        public EventUnwinder(
            ProjectionEngineConfig config,
            IPersistence persistence,
            ILogger logger)
        {
            _config = config;
            _persistence = persistence;
            _logger = logger;

            var url = new MongoUrl(_config.EventStoreConnectionString);
            var client = url.CreateClient(false);
            var _mongoDatabase = client.GetDatabase(url.DatabaseName);

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

        /// <summary>
        /// This contains all the headers of the event that we want to remove.
        /// </summary>
        public HashSet<string> HeaderToRemove { get; set; } = new HashSet<string>();

        public IMongoCollection<UnwindedDomainEvent> UnwindedCollection
        {
            get { return _unwindedEventCollection; }
        }

        public async Task UnwindAsync()
        {
            var lastEvent = _unwindedEventCollection
                .Find(Builders<UnwindedDomainEvent>.Filter.Empty)
                .Sort(Builders<UnwindedDomainEvent>.Sort.Descending(e => e.CheckpointToken))
                .Limit(1)
                .SingleOrDefault();
            Int64 startToken = 1; //remember with NStore is inclusive
            if (lastEvent != null)
            {
                startToken = lastEvent.CheckpointToken;
            }

            //we want to avoid holes up to the maximum committed tokens, holes are only when we are
            //in high concurrency.
            var lastCommittedToken = await _persistence.ReadLastPositionAsync().ConfigureAwait(false);
            lastCommittedToken = Math.Max(0, lastCommittedToken - 10); //we choose an arbitrary offset to avoid problem in high concurrency

            _logger.InfoFormat("Unwind events starting from commit {0}", startToken);

            //Since we could have crashed during unwind of last commit, we want to reunwind again last commit
            Int64 checkpointToken = startToken;
            _unwindedEventCollection.DeleteMany(Builders<UnwindedDomainEvent>.Filter.Gte(e => e.CheckpointToken, startToken));

            Int64 count = 0;
            List<UnwindedDomainEvent> batchEventUnwind = new List<UnwindedDomainEvent>(550);

            var subscription = new JarvisFrameworkLambdaSubscription(async data =>
            {
                foreach (var evt in UnwindChunk(data))
                {
                    batchEventUnwind.Add(evt);
                    if (batchEventUnwind.Count > 500)
                    {
                        await _unwindedEventCollection.InsertManyAsync(batchEventUnwind).ConfigureAwait(false);
                        batchEventUnwind.Clear();
                    }
                }
                checkpointToken = data.Position;
                if (++count % 10000 == 0)
                {
                    _logger.InfoFormat("Unwinded block of 10000 commit, now we are at checkpoint {0}", checkpointToken);
                }

                return true;
            }, "unwinder");

            var sequencer = new NStoreSequencer(
                startToken - 1,
                subscription,
                timeToWaitInMilliseconds: 0,
                maximumWaitCountForSingleHole: 5,
                safePosition: lastCommittedToken,
                logger: _logger);

            //Need to cycle until the sequencer skipped an hole, to be sure to unwind all events.
            do
            {
                //Reload from the last position where the sequencer stopped.
                await _persistence.ReadAllAsync(sequencer.Position + 1, sequencer).ConfigureAwait(false);
            } while (sequencer.RetriesOnHole > 0);

            if (subscription.Failed)
            {
                throw new JarvisFrameworkEngineException("Error unwinding commits", subscription.LastException);
            }

            //Flush last batch.
            if (batchEventUnwind.Count > 0)
            {
                _unwindedEventCollection.InsertMany(batchEventUnwind);
            }
            _logger.InfoFormat("Unwind events ends, started from commit {0} and ended with commit {1}", startToken, checkpointToken);
        }

        private IEnumerable<UnwindedDomainEvent> UnwindChunk(IChunk chunk)
        {
            Changeset commit = chunk.Payload as Changeset;
            if (commit != null)
            {
                Int32 ordinal = 1;
                foreach (var evt in commit.Events)
                {
                    var id = chunk.Position + "_" + ordinal;
                    UnwindedDomainEvent unwinded = new UnwindedDomainEvent();
                    unwinded.Id = id;
                    unwinded.CheckpointToken = chunk.Position;
                    unwinded.EventSequence = ordinal;
                    unwinded.EventType = evt.GetType().Name;
                    unwinded.Event = evt;

                    //save all data from commit to the event unwinded to do enhance after load.
                    var headers = commit.Headers;
                    unwinded.CommitStamp = (evt as DomainEvent)?.CommitStamp ?? DateTime.MinValue;
                    unwinded.CommitId = chunk.OperationId;
                    unwinded.Version = commit.AggregateVersion;
                    unwinded.PartitionId = chunk.PartitionId;

                    var newContext = new Dictionary<String, Object>();
                    foreach (var key in headers.Keys.Where(k => !HeaderToRemove.Contains(k)))
                    {
                        newContext[key] = headers[key];
                    }
                    unwinded.Context = newContext;

                    yield return unwinded;
                    ordinal++;
                }
            }
            else if (chunk.Payload != null)
            {
                var id = chunk.Position + "_1";
                UnwindedDomainEvent unwinded = new UnwindedDomainEvent();
                unwinded.Id = id;
                unwinded.CheckpointToken = chunk.Position;
                unwinded.EventSequence = 1;
                unwinded.EventType = chunk.Payload.GetType().Name;
                unwinded.Event = chunk.Payload;

                //save all data from commit to the event unwinded to do enhance after load.
                unwinded.CommitStamp = DateTime.MinValue;
                unwinded.CommitId = chunk.OperationId;
                unwinded.Version = 0;
                unwinded.PartitionId = chunk.PartitionId;
                unwinded.Context = new Dictionary<String, Object>();

                yield return unwinded;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.MultitenantSupport;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using NEventStore;
using NEventStore.Client;
using NEventStore.Serialization;
using NEventStore.Persistence;
using Jarvis.Framework.Kernel.Support;
using System.Collections.Concurrent;
using System.Text;
using Jarvis.Framework.Shared.Helpers;
using System.Threading.Tasks.Dataflow;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    public class RebuildProjectionEngine
    {
        readonly EventUnwinder _eventUnwinder;

        readonly IConcurrentCheckpointTracker _checkpointTracker;

        private readonly ProjectionMetrics _metrics;

        public IExtendedLogger Logger
        {
            get { return _logger; }
            set { _logger = value; }
        }
        IExtendedLogger _logger = NullLogger.Instance;

        public ILoggerFactory LoggerFactory
        {
            get { return _loggerFactory; }
            set { _loggerFactory = value; }
        }
        private ILoggerFactory _loggerFactory;

        readonly ProjectionEngineConfig _config;
        readonly Dictionary<string, IProjection[]> _projectionsBySlot;
        private readonly IProjection[] _allProjections;

        private readonly IRebuildContext _rebuildContext;
        private ProjectionEventInspector _projectionInspector;

        private int _bufferSize = 4000;

        public RebuildProjectionEngine(
            EventUnwinder eventUnwinder,
            IConcurrentCheckpointTracker checkpointTracker,
            IProjection[] projections,
            IRebuildContext rebuildContext,
            ProjectionEngineConfig config,
            ProjectionEventInspector projectionInspector)
        {
            _eventUnwinder = eventUnwinder;
            _checkpointTracker = checkpointTracker;
            _rebuildContext = rebuildContext;
            _config = config;
            _projectionInspector = projectionInspector;

            if (_config.Slots[0] != "*")
            {
                projections = projections
                    .Where(x => _config.Slots.Any(y => y == x.Info.SlotName))
                    .ToArray();
            }

            _allProjections = projections;
            _projectionsBySlot = projections
                .GroupBy(x => x.Info.SlotName)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.Priority).ToArray());

            _metrics = new ProjectionMetrics(_allProjections);
        }

        private void DumpProjections()
        {
            if (Logger.IsDebugEnabled)
            {
                foreach (var prj in _allProjections)
                {
                    if (Logger.IsDebugEnabled) Logger.DebugFormat("Projection: {0}", prj.GetType().FullName);
                }

                if (_allProjections.Length == 0)
                {
                    Logger.Warn("no projections found!");
                }
            }
        }

        private readonly Dictionary<BucketInfo, List<RebuildProjectionSlotDispatcher>> _consumers = new Dictionary<BucketInfo, List<RebuildProjectionSlotDispatcher>>();
        private readonly List<RebuildProjectionSlotDispatcher> _rebuildDispatchers = new List<RebuildProjectionSlotDispatcher>();

        RebuildStatus _status;

        public async Task<RebuildStatus> RebuildAsync()
        {
            if (Logger.IsInfoEnabled) Logger.InfoFormat("Starting rebuild projection engine on tenant {0}", _config.TenantId);
            DumpProjections();

            _eventUnwinder.Unwind();

            _status = new RebuildStatus();
            TenantContext.Enter(_config.TenantId);

            await ConfigureProjections().ConfigureAwait(false);

            var allSlots = _projectionsBySlot.Keys.ToArray();

            //initialize dispatching of the commits
            foreach (var bucket in _config.BucketInfo)
            {
                _consumers.Add(bucket, new List<RebuildProjectionSlotDispatcher>());
                _status.AddBucket();
            }

            //Setup the slots
            foreach (var slotName in allSlots)
            {
                var startCheckpoint = GetStartCheckpointForSlot(slotName);
                Logger.InfoFormat("Slot {0} starts from {1}", slotName, startCheckpoint);

                var projectionsForThisSlot = _projectionsBySlot[slotName];
                Int64 maximumDispatchedValue = projectionsForThisSlot
                    .Select(p => _checkpointTracker.GetCheckpoint(p))
                    .Max();
                var dispatcher = new RebuildProjectionSlotDispatcher(_logger, slotName, _config, projectionsForThisSlot, _checkpointTracker, maximumDispatchedValue);
                MetricsHelper.SetCheckpointCountToDispatch(slotName, () => dispatcher.CheckpointToDispatch);
                _rebuildDispatchers.Add(dispatcher);

                //find right consumer
                var slotBucket = _config.BucketInfo.SingleOrDefault(b =>
                    b.Slots.Any(s => s.Equals(slotName, StringComparison.OrdinalIgnoreCase))) ??
                    _config.BucketInfo.Single(b => b.Slots[0] == "*");
                var consumerList = _consumers[slotBucket];
                consumerList.Add(dispatcher);
            }

            //now start tpl and start polling in other threads.
            foreach (var consumer in _consumers)
            {
                var bucketInfo = String.Join(",", consumer.Key.Slots);

                if (consumer.Value.Count == 0)
                {
                    _logger.InfoFormat("Bucket {0} has no active slot, and will be ignored!", bucketInfo);
                    _status.BucketDone(bucketInfo, 0, 0, 0);
                    continue;
                }
                DataflowBlockOptions consumerBufferOptions = new DataflowBlockOptions();
                consumerBufferOptions.BoundedCapacity = _bufferSize;
                var _buffer = new BufferBlock<DomainEvent>(consumerBufferOptions);

                ExecutionDataflowBlockOptions executionOption = new ExecutionDataflowBlockOptions();
                executionOption.BoundedCapacity = _bufferSize;

                var dispatcherList = consumer.Value;
                _projectionInspector.ResetHandledEvents();
                List<ActionBlock<DomainEvent>> consumers = new List<ActionBlock<DomainEvent>>();
                foreach (var dispatcher in dispatcherList)
                {
                    ExecutionDataflowBlockOptions consumerOptions = new ExecutionDataflowBlockOptions();
                    consumerOptions.BoundedCapacity = _bufferSize;
                    var actionBlock = new ActionBlock<DomainEvent>((Func<DomainEvent, Task>)dispatcher.DispatchEventAsync, consumerOptions);
                    consumers.Add(actionBlock);
                    foreach (var projection in dispatcher.Projections)
                    {
                        _projectionInspector.InspectProjectionForEvents(projection.GetType());
                    }
                }
                var allTypeHandledStringList = _projectionInspector.EventHandled.Select(t => t.Name).ToList();

                var _broadcaster = GuaranteedDeliveryBroadcastBlock.Create(consumers, bucketInfo, 3000);
                _buffer.LinkTo(_broadcaster, new DataflowLinkOptions() { PropagateCompletion = true });

                //Intentionally not waiting, just start polling in another thread.
                StartPoll(_buffer, _broadcaster, bucketInfo, dispatcherList, allTypeHandledStringList, consumers);
            }

            MetricsHelper.SetProjectionEngineCurrentDispatchCount(() => RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);
            return _status;
        }

        private async Task StartPoll(
            BufferBlock<DomainEvent> buffer,
            ActionBlock<DomainEvent> broadcaster,
            String bucketInfo,
            List<RebuildProjectionSlotDispatcher> dispatchers,
            List<String> allEventTypesHandledByAllSlots,
            List<ActionBlock<DomainEvent>> consumers)
        {
            Stopwatch sw = Stopwatch.StartNew();

            var eventCollection = _eventUnwinder.UnwindedCollection;
            Int64 lastCheckpointTokenDispatched = 0;
            Int32 lastSequenceDispatched = 0;
            Int64 maxEventDispatched = dispatchers.Max(d => d.LastCheckpointDispatched);
            Int64 totalEventToDispatch = eventCollection.Count(Builders<UnwindedDomainEvent>.Filter.Lte(e => e.CheckpointToken, maxEventDispatched));
            Int64 dispatchedEvents = 0;

            //this is the main cycle that continue to poll events and send to the tpl buffer
            Boolean done = false;
            while (!done)
            {
                try
                {
                    var query = eventCollection
                        .Find(
                                //Greater than last dispatched, less than maximum dispatched and one of the events that are elaborated by at least one of the projection
                                Builders<UnwindedDomainEvent>.Filter.And(
                                    Builders<UnwindedDomainEvent>.Filter.Or( //this is the condition on last dispatched
                                        Builders<UnwindedDomainEvent>.Filter.Gt(e => e.CheckpointToken, lastCheckpointTokenDispatched), //or checkpoint token is greater than last dispatched.
                                        Builders<UnwindedDomainEvent>.Filter.And( //or is equal to the last dispatched, but the EventSequence is greater.
                                            Builders<UnwindedDomainEvent>.Filter.Gt(e => e.EventSequence, lastSequenceDispatched),
                                            Builders<UnwindedDomainEvent>.Filter.Eq(e => e.CheckpointToken, lastCheckpointTokenDispatched)
                                        )
                                    ),
                                    Builders<UnwindedDomainEvent>.Filter.Lte(e => e.CheckpointToken, maxEventDispatched), //less than or equal max event dispatched.
                                    Builders<UnwindedDomainEvent>.Filter.Or( //should be one of the handled type, or the last one.
                                        Builders<UnwindedDomainEvent>.Filter.In(e => e.EventType, allEventTypesHandledByAllSlots),
                                        Builders<UnwindedDomainEvent>.Filter.Eq(e => e.CheckpointToken, maxEventDispatched)
                                    )
                             )
                        )
                        .Sort(Builders<UnwindedDomainEvent>.Sort.Ascending(e => e.CheckpointToken).Ascending(e => e.EventSequence));
                    foreach (var eventUnwinded in query.ToEnumerable())
                    {
                        var evt = eventUnwinded.GetEvent();
                        if (_logger.IsDebugEnabled) _logger.DebugFormat("TPL queued event {0}/{1} for bucket {2}", eventUnwinded.CheckpointToken, eventUnwinded.EventSequence, bucketInfo);
                        var task = buffer.SendAsync(evt);
                        //wait till the commit is stored in tpl queue
                        task.Wait();
                        dispatchedEvents++;
                        lastSequenceDispatched = eventUnwinded.EventSequence;
                        lastCheckpointTokenDispatched = eventUnwinded.CheckpointToken;
                    }
                    done = true;
                }
                catch (MongoException ex)
                {
                    _logger.WarnFormat(ex, "Unable to poll event from UnwindedCollection: {0} - Last good checkpoint dispatched {1}", ex.Message, lastCheckpointTokenDispatched);
                }
                catch (Exception ex)
                {
                    _logger.FatalFormat(ex, "Unable to poll event from UnwindedCollection: {0} - Last good checkpoint dispatched {1}", ex.Message, lastCheckpointTokenDispatched);
                }
            }

            try
            {
                _logger.InfoFormat("Finished loading events for bucket {0} wait for tpl to finish flush", bucketInfo);
                buffer.Complete();
                broadcaster.Completion.Wait(); //wait for all event to be broadcasted.
                Task.WaitAll(consumers.Select(c => c.Completion).ToArray()); //wait for all consumers to complete.

                Thread.Sleep(1000); //wait for another secondo before finishing.

                //create a list of dispatcher to wait for finish
                List<RebuildProjectionSlotDispatcher> dispatcherWaitingToFinish = dispatchers.ToList();
                while (dispatcherWaitingToFinish.Count > 0)
                {
                    if (_logger.IsDebugEnabled) _logger.DebugFormat("Waiting for {0} slot to finish events", dispatcherWaitingToFinish.Count);
                    Int32 i = dispatcherWaitingToFinish.Count - 1;
                    while (i >= 0)
                    {
                        var dispatcher = dispatcherWaitingToFinish[i];
                        if (dispatcher.Finished)
                        {
                            foreach (var projection in dispatcher.Projections)
                            {
                                try
                                {
                                    await projection.StopRebuildAsync().ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    _logger.ErrorFormat(ex, "Error in StopRebuild for projection {0}", projection.Info.CommonName);
                                    throw;
                                }

                                var meter = _metrics.GetMeter(projection.Info.CommonName);
                                _checkpointTracker.RebuildEnded(projection, meter);

                                Logger.InfoFormat("Rebuild done for projection {0} @ {1}",
                                    projection.GetType().FullName,
                                    dispatcher.LastCheckpointDispatched
                                );
                            }
                            _checkpointTracker.UpdateSlotAndSetCheckpoint(
                                dispatcher.SlotName,
                                dispatcher.Projections.Select(p => p.Info.CommonName),
                                dispatcher.LastCheckpointDispatched);

                            dispatcherWaitingToFinish.Remove(dispatcher);
                            Logger.InfoFormat("Rebuild ended for slot {0}", dispatcher.SlotName);
                        }

                        i--;
                    }
                    Thread.Sleep(2000);
                }
                sw.Stop();
                _logger.InfoFormat("Bucket {0} finished dispathing all events for slots: {1}", bucketInfo, dispatchers.Select(d => d.SlotName).Aggregate((s1, s2) => s1 + ", " + s2));
                _status.BucketDone(bucketInfo, dispatchedEvents, sw.ElapsedMilliseconds, totalEventToDispatch);
            }
            catch (Exception ex)
            {
                _logger.FatalFormat(ex, "Error during rebuild finish {0}", ex.Message);
                throw;
            }
        }

        Int64 GetStartGlobalCheckpoint()
        {
            var checkpoints = _projectionsBySlot.Keys.Select(GetStartCheckpointForSlot).Distinct().ToArray();
            var min = checkpoints.Any() ? checkpoints.Min() : 0;

            return min;
        }

        Int64 GetStartCheckpointForSlot(string slotName)
        {
            Int64 min = Int64.MaxValue;

            var projections = _projectionsBySlot[slotName];
            foreach (var projection in projections)
            {
                if (_checkpointTracker.NeedsRebuild(projection))
                    return 0;

                var currentValue = _checkpointTracker.GetCurrent(projection);
                if (currentValue < min)
                {
                    min = currentValue;
                }
            }

            return min;
        }

        private async Task ConfigureProjections()
        {
            _checkpointTracker.SetUp(_allProjections, 1, false);
            var lastCommit = GetLastCommitId();
            foreach (var slot in _projectionsBySlot)
            {
                var maxCheckpointDispatchedInSlot = slot.Value
                    .Select(projection =>
                    {
                        var checkpoint = _checkpointTracker.GetCheckpoint(projection);
                        var longCheckpoint = checkpoint;
                        return longCheckpoint;
                    })
                    .Max();

                foreach (var projection in slot.Value)
                {
                    //Check if this slot has ever dispatched at least one commit
                    if (maxCheckpointDispatchedInSlot > 0)
                    {
                        _checkpointTracker.SetCheckpoint(projection.Info.CommonName,
                            maxCheckpointDispatchedInSlot);
                        await projection.DropAsync().ConfigureAwait(false);
                        await projection.StartRebuildAsync(_rebuildContext).ConfigureAwait(false);
                        _checkpointTracker.RebuildStarted(projection, lastCommit);
                    }
                    else
                    {
                        //this is a new slot, all the projection should execute
                        //as if they are not in rebuild, because they are new
                        //so we need to immediately stop rebuilding.
                        await projection.StopRebuildAsync().ConfigureAwait(false);
                    }
                    await projection.SetUpAsync().ConfigureAwait(false);
                }
            }
            //Standard projection engine now executes check for GetCheckpointErrors but
            //it is not useful here, because we are in rebuild, so every error will be fixed.
        }

        private Int64 GetLastCommitId()
        {
            var collection = GetMongoCommitsCollection();
            var lastCommit = collection
                .Find(Builders<BsonDocument>.Filter.Gte("_id", 0))
                .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                .Limit(1)
                .FirstOrDefault();

            if (lastCommit == null) return 0;

            return lastCommit["_id"].AsInt64;
        }

        private readonly ConcurrentDictionary<String, Int64> lastCheckpointDispatched = new ConcurrentDictionary<string, long>();

        private IMongoCollection<BsonDocument> GetMongoCommitsCollection()
        {
            var url = new MongoUrl(_config.EventStoreConnectionString);
            var client = new MongoClient(url);
            var db = client.GetDatabase(url.DatabaseName);
            return db.GetCollection<BsonDocument>("Commits");
        }
    }

    #region metrics 

    internal static class RebuildProjectionMetrics
    {
        internal static Int32 CountOfConcurrentDispatchingCommit = 0;
    }

    #endregion
}

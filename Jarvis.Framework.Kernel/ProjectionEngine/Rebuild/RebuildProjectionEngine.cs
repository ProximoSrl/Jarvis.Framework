using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Support;
using Metrics;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    public class RebuildProjectionEngine
    {
        private readonly EventUnwinder _eventUnwinder;

        private readonly IConcurrentCheckpointTracker _checkpointTracker;

        private readonly ProjectionMetrics _metrics;

        public ILogger Logger { get; set; } = NullLogger.Instance;

        private readonly ProjectionEngineConfig _config;
        private readonly Dictionary<string, IProjection[]> _projectionsBySlot;
        private readonly IProjection[] _allProjections;

        private readonly IRebuildContext _rebuildContext;
        private readonly ProjectionEventInspector _projectionInspector;

        public RebuildProjectionEngine(
            EventUnwinder eventUnwinder,
            IConcurrentCheckpointTracker checkpointTracker,
            IProjection[] projections,
            IRebuildContext rebuildContext,
            ProjectionEngineConfig config,
            ProjectionEventInspector projectionInspector,
            ILoggerThreadContextManager loggerThreadContextManager)
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
            _loggerThreadContextManager = loggerThreadContextManager;

            HealthChecks.RegisterHealthCheck("RebuildProjectionEngine", (Func<HealthCheckResult>)HealthCheck);
        }

        private HealthCheckResult HealthCheck()
        {
            if (String.IsNullOrEmpty(_pollError))
            {
                return HealthCheckResult.Healthy();
            }
            return HealthCheckResult.Unhealthy(_pollError);
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

        private readonly Dictionary<BucketInfo, List<RebuildProjectionSlotDispatcher>> _consumers =
            new Dictionary<BucketInfo, List<RebuildProjectionSlotDispatcher>>();
        private readonly List<RebuildProjectionSlotDispatcher> _rebuildDispatchers =
            new List<RebuildProjectionSlotDispatcher>();

        private RebuildStatus _status;

        /// <summary>
        /// Start rebuild process.
        /// </summary>
        /// <returns></returns>
        public async Task<RebuildStatus> RebuildAsync()
        {
            if (Logger.IsInfoEnabled) Logger.InfoFormat("Starting rebuild projection engine on tenant {0}", _config.TenantId);
            DumpProjections();

            await _eventUnwinder.UnwindAsync().ConfigureAwait(false);

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
                Logger.InfoFormat("Slot {0} will be rebuilded", slotName);

                var projectionsForThisSlot = _projectionsBySlot[slotName];
                Int64 maximumDispatchedValue = projectionsForThisSlot
                    .Select(p => _checkpointTracker.GetCheckpoint(p))
                    .Max();
                var dispatcher = new RebuildProjectionSlotDispatcher(Logger, slotName, _config, projectionsForThisSlot, maximumDispatchedValue, _loggerThreadContextManager);
                KernelMetricsHelper.SetCheckpointCountToDispatch(slotName, () => dispatcher.CheckpointToDispatch);
                _rebuildDispatchers.Add(dispatcher);

                //find right consumer
                var slotBucket = _config.BucketInfo.SingleOrDefault(b =>
                    b.Slots.Any(s => s.Equals(slotName, StringComparison.OrdinalIgnoreCase))) ??
                    _config.BucketInfo.Single(b => b.Slots[0] == "*");
                var consumerList = _consumers[slotBucket];
                consumerList.Add(dispatcher);
            }

            //Creates TPL chain and start polling everything on the consumer
            foreach (var consumer in _consumers)
            {
                var bucketInfo = String.Join(",", consumer.Key.Slots);

                if (consumer.Value.Count == 0)
                {
                    Logger.InfoFormat("Bucket {0} has no active slot, and will be ignored!", bucketInfo);
                    _status.BucketDone(bucketInfo, 0, 0, 0);
                    continue;
                }
                var consumerBufferOptions = new DataflowBlockOptions();
                consumerBufferOptions.BoundedCapacity = consumer.Key.BufferSize;
                var _buffer = new BufferBlock<UnwindedDomainEvent>(consumerBufferOptions);

                ExecutionDataflowBlockOptions executionOption = new ExecutionDataflowBlockOptions();
                executionOption.BoundedCapacity = consumer.Key.BufferSize;

                var dispatcherList = consumer.Value;
                _projectionInspector.ResetHandledEvents();
                List<SlotGuaranteedDeliveryBroadcastBlock.SlotInfo<UnwindedDomainEvent>> consumers =
                    new List<SlotGuaranteedDeliveryBroadcastBlock.SlotInfo<UnwindedDomainEvent>>();
                foreach (var dispatcher in dispatcherList)
                {
                    ExecutionDataflowBlockOptions consumerOptions = new ExecutionDataflowBlockOptions();
                    consumerOptions.BoundedCapacity = consumer.Key.BufferSize;
                    var actionBlock = new ActionBlock<UnwindedDomainEvent>((Func<UnwindedDomainEvent, Task>)dispatcher.DispatchEventAsync, consumerOptions);
                    HashSet<Type> eventsOfThisSlot = new HashSet<Type>();
                    foreach (var projection in dispatcher.Projections)
                    {
                        var domainEventTypesHandledByThisProjection = _projectionInspector.InspectProjectionForEvents(projection.GetType());
                        foreach (var type in domainEventTypesHandledByThisProjection)
                        {
                            eventsOfThisSlot.Add(type);
                        }
                    }

                    SlotGuaranteedDeliveryBroadcastBlock.SlotInfo<UnwindedDomainEvent> slotInfo =
                        new SlotGuaranteedDeliveryBroadcastBlock.SlotInfo<UnwindedDomainEvent>(
                            actionBlock,
                            dispatcher.SlotName,
                            eventsOfThisSlot);
                    consumers.Add(slotInfo);
                    KernelMetricsHelper.CreateMeterForRebuildDispatcherBuffer(dispatcher.SlotName, () => actionBlock.InputCount);
                }
                var allTypeHandledStringList = _projectionInspector.EventHandled.Select(t => t.Name).ToList();

                var broadcaster = SlotGuaranteedDeliveryBroadcastBlock.Create(consumers, bucketInfo, consumer.Key.BufferSize);
                _buffer.LinkTo(broadcaster, new DataflowLinkOptions() { PropagateCompletion = true });

                KernelMetricsHelper.CreateGaugeForRebuildFirstBuffer(bucketInfo, () => _buffer.Count);
                KernelMetricsHelper.CreateGaugeForRebuildBucketDBroadcasterBuffer(bucketInfo, () => broadcaster.InputCount);

                KernelMetricsHelper.CreateMeterForRebuildEventCompleted(bucketInfo);

                //fire each bucket in own thread
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Factory.StartNew(() => StartPoll(
                    _buffer,
                    broadcaster,
                    bucketInfo,
                    dispatcherList,
                    allTypeHandledStringList,
                    consumers));

                //await StartPoll(_buffer, _broadcaster, bucketInfo, dispatcherList, allTypeHandledStringList, consumers).ConfigureAwait(false);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }

            KernelMetricsHelper.SetProjectionEngineCurrentDispatchCount(() => RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);
            return _status;
        }

        private String _pollError = null;

        private async Task StartPoll(
            BufferBlock<UnwindedDomainEvent> buffer,
            ActionBlock<UnwindedDomainEvent> broadcaster,
            String bucketInfo,
            List<RebuildProjectionSlotDispatcher> dispatchers,
            List<String> allEventTypesHandledByAllSlots,
            List<SlotGuaranteedDeliveryBroadcastBlock.SlotInfo<UnwindedDomainEvent>> consumers)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (broadcaster == null)
                throw new ArgumentNullException(nameof(broadcaster));

            if (dispatchers == null)
                throw new ArgumentNullException(nameof(dispatchers));

            if (consumers == null)
                throw new ArgumentNullException(nameof(consumers));

            if (allEventTypesHandledByAllSlots == null)
                throw new ArgumentNullException(nameof(allEventTypesHandledByAllSlots));

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
                    IFindFluent<UnwindedDomainEvent, UnwindedDomainEvent> filter = eventCollection
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
                                    Builders<UnwindedDomainEvent>.Filter.Lte(e => e.CheckpointToken, maxEventDispatched) //less than or equal max event dispatched.
                                    , Builders<UnwindedDomainEvent>.Filter.In(e => e.EventType, allEventTypesHandledByAllSlots)
                             )
                        );

                    Logger.InfoFormat($"polled rebuild query {filter}");
                    var query = filter
                        .Sort(Builders<UnwindedDomainEvent>.Sort.Ascending(e => e.CheckpointToken).Ascending(e => e.EventSequence));
                    await query.ForEachAsync(Dispatch).ConfigureAwait(false);

                    async Task Dispatch(UnwindedDomainEvent eventUnwinded)
                    {
                        _pollError = null;

                        if (Logger.IsDebugEnabled) Logger.DebugFormat("TPL queued event {0}/{1} for bucket {2}", eventUnwinded.CheckpointToken, eventUnwinded.EventSequence, bucketInfo);
                        //rehydrate the event before dispatching
                        eventUnwinded.EnhanceAndUpcastEvent();
                        await buffer.SendAsync(eventUnwinded).ConfigureAwait(false);

                        dispatchedEvents++;
                        lastSequenceDispatched = eventUnwinded.EventSequence;
                        lastCheckpointTokenDispatched = eventUnwinded.CheckpointToken;
                    }

                    //Send marker unwindws domain event to signal that rebuild is finished
                    await buffer.SendAsync(UnwindedDomainEvent.LastEvent).ConfigureAwait(false);
                    done = true;
                }
                catch (MongoException ex)
                {
                    _pollError = $"Unable to poll event from UnwindedCollection: {ex.Message} - Last good checkpoint dispatched { lastCheckpointTokenDispatched }";
                    Logger.WarnFormat(ex, "Unable to poll event from UnwindedCollection: {0} - Last good checkpoint dispatched {1}", ex.Message, lastCheckpointTokenDispatched);
                }
                catch (Exception ex)
                {
                    _pollError = $"Unable to poll event from UnwindedCollection: {ex.Message} - Last good checkpoint dispatched { lastCheckpointTokenDispatched }";
                    Logger.FatalFormat(ex, "Unable to poll event from UnwindedCollection: {0} - Last good checkpoint dispatched {1}", ex.Message, lastCheckpointTokenDispatched);
                    throw;
                }
            }

            try
            {
                Logger.InfoFormat("Finished loading events for bucket {0} wait for tpl to finish flush", bucketInfo);
                buffer.Complete();
                broadcaster.Completion.Wait(); //wait for all event to be broadcasted.
                Task.WaitAll(consumers.Select(c => c.Target.Completion).ToArray()); //wait for all consumers to complete.

                Thread.Sleep(1000); //wait for another secondo before finishing.

                //create a list of dispatcher to wait for finish, then start waiting.
                List<RebuildProjectionSlotDispatcher> dispatcherWaitingToFinish = dispatchers.ToList();
                while (dispatcherWaitingToFinish.Count > 0)
                {
                    if (Logger.IsDebugEnabled) Logger.DebugFormat("Waiting for {0} slot to finish events", dispatcherWaitingToFinish.Count);
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
                                    Logger.ErrorFormat(ex, "Error in StopRebuild for projection {0}", projection.Info.CommonName);
                                    throw;
                                }

                                var meter = _metrics.GetMeter(projection.Info.CommonName);
                                _checkpointTracker.RebuildEnded(projection, meter);

                                Logger.InfoFormat("Rebuild done for projection {0} @ {1}",
                                    projection.GetType().FullName,
                                    dispatcher.LastCheckpointDispatched
                                );
                            }
                            await _checkpointTracker.UpdateSlotAndSetCheckpointAsync(
                                dispatcher.SlotName,
                                dispatcher.Projections.Select(p => p.Info.CommonName),
                                dispatcher.LastCheckpointDispatched).ConfigureAwait(false);

                            dispatcherWaitingToFinish.Remove(dispatcher);
                            Logger.InfoFormat("Rebuild ended for slot {0}", dispatcher.SlotName);
                        }

                        i--;
                    }
                    if (dispatcherWaitingToFinish.Count > 0) Thread.Sleep(2000);
                }
                sw.Stop();
                Logger.InfoFormat("Bucket {0} finished dispathing all events for slots: {1}", bucketInfo, dispatchers.Select(d => d.SlotName).Aggregate((s1, s2) => s1 + ", " + s2));
                _status.BucketDone(bucketInfo, dispatchedEvents, sw.ElapsedMilliseconds, totalEventToDispatch);
            }
            catch (Exception ex)
            {
                Logger.FatalFormat(ex, "Error during rebuild finish {0}", ex.Message);
                throw;
            }
        }

        private async Task ConfigureProjections()
        {
            _checkpointTracker.SetUp(_allProjections, 1, false);
            var lastCommit = GetLastCommitId();
            foreach (var slot in _projectionsBySlot)
            {
                foreach (var projection in slot.Value)
                {
                    await projection.DropAsync().ConfigureAwait(false);
                    await projection.StartRebuildAsync(_rebuildContext).ConfigureAwait(false);
                    _checkpointTracker.RebuildStarted(projection, lastCommit);
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

        private readonly ILoggerThreadContextManager _loggerThreadContextManager;

        private IMongoCollection<BsonDocument> GetMongoCommitsCollection()
        {
            var url = new MongoUrl(_config.EventStoreConnectionString);
            var client = url.CreateClient(false);
            var db = client.GetDatabase(url.DatabaseName);
            return db.GetCollection<BsonDocument>(EventStoreFactory.PartitionCollectionName);
        }
    }

    #region metrics 

    internal static class RebuildProjectionMetrics
    {
#pragma warning disable S2223 // Non-constant static fields should not be visible
        internal static Int32 CountOfConcurrentDispatchingCommit = 0;
#pragma warning restore S2223 // Non-constant static fields should not be visible
    }

    #endregion
}

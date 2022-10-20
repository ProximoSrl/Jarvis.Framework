using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.HealthCheck;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using NStore.Core.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NStore.Domain;
using Jarvis.Framework.Shared.Store;
using NStore.Persistence.Mongo;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    /// <summary>
    /// <para>
    /// New version of the RebuildProjectionEngine that does not use
    /// Unwinded collection but instead simply filter commit with events
    /// to dispatch using a filter in a nested property Payload.Events._t allowing
    /// saving the huge amount of space that the unwinded events collection requires.
    /// </para>
    /// <para>
    /// This follows the standard workflow for rebuild projection engine, just
    /// put all the projection/slots in rebuild, then re-dispatch everything up to
    /// the last dispatched event for each slot. Then when the rebuild finishes, all
    /// in-memories collection will be flushed on database
    /// </para>
    /// </summary>
    public class RebuildProjectionEngineV2 : IRebuildEngine
    {
        private readonly IConcurrentCheckpointTracker _checkpointTracker;

        private readonly ProjectionMetrics _metrics;

        public ILogger Logger { get; set; } = NullLogger.Instance;

        private readonly ProjectionEngineConfig _config;

        /// <summary>
        /// This dictionary contains slot name as key and an array of projections
        /// corresponding to that slot. Needed to being able to understant the list
        /// of projection that belongs to each slot.
        /// </summary>
        private readonly Dictionary<string, IProjection[]> _projectionsBySlot;

        private readonly IProjection[] _allProjections;

        private readonly IRebuildContext _rebuildContext;
        private readonly ProjectionEventInspector _projectionInspector;

        private readonly ICommitEnhancer _commitEnhancer;
        private readonly IMongoCollection<MongoChunk> _commitCollection;

        public RebuildProjectionEngineV2(
            IConcurrentCheckpointTracker checkpointTracker,
            IProjection[] projections,
            IRebuildContext rebuildContext,
            ProjectionEngineConfig config,
            ProjectionEventInspector projectionInspector,
            ICommitEnhancer commitEnhancer,
            IMongoDatabase commitDatabase)
        {
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
            _commitEnhancer = commitEnhancer;

            _commitCollection = commitDatabase.GetCollection<MongoChunk>("Commits");

            JarvisFrameworkHealthChecks.RegisterHealthCheck("RebuildProjectionEngineV2", HealthCheck);
        }

        private JarvisFrameworkHealthCheckResult HealthCheck()
        {
            if (String.IsNullOrEmpty(_pollError))
            {
                return JarvisFrameworkHealthCheckResult.Healthy();
            }
            return JarvisFrameworkHealthCheckResult.Unhealthy(_pollError);
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

        /// <summary>
        /// One entry for each bucket. A bucket is the unit of polling, each bucket has a different
        /// poller that serves a list of slot. Value of this array is a list of RebuildProjectionSlotDispatcher objects
        /// that have the logic to dispatch a <see cref="DomainEvent"/> or <see cref="UnwindedDomainEvent"/> to 
        /// corresponding slots projection
        /// </summary>
        private readonly Dictionary<BucketInfo, List<RebuildProjectionSlotDispatcher>> _consumers =
            new Dictionary<BucketInfo, List<RebuildProjectionSlotDispatcher>>();

        /// <summary>
        /// This is the shared status object that is used to signal to outher world if the rebuild finished.
        /// </summary>
        private RebuildStatus _status;

        /// <summary>
        /// Start rebuild process, configure everything, then return an object that can be used to know the
        /// status of the projection engine.
        /// </summary>
        /// <returns></returns>
        public async Task<RebuildStatus> RebuildAsync()
        {
            if (Logger.IsInfoEnabled) Logger.InfoFormat("Starting rebuild projection engine on tenant {0}", _config.TenantId);
            DumpProjections();

            // We need to be sure to have an index that supports filtering events type in commits, or we will really poll slowly.
            await EnsureIndexAsync();

            _status = new RebuildStatus();
            TenantContext.Enter(_config.TenantId);

            //Configure projections
            await ConfigureProjections().ConfigureAwait(false);

            //Group by projection by slot, so we have all slots that needs to be rebuilded
            var allSlots = _projectionsBySlot.Keys.ToArray();

            //initialize dispatching of the commits, remember that we have a poller for each bucket.
            foreach (var bucket in _config.BucketInfo)
            {
                _consumers.Add(bucket, new List<RebuildProjectionSlotDispatcher>());
                _status.AddBucket(String.Join(",", bucket.Slots));
            }

            //Setup the slots for dispatching the events.
            foreach (var slotName in allSlots)
            {
                Logger.InfoFormat("Slot {0} will be rebuilded", slotName);

                var projectionsForThisSlot = _projectionsBySlot[slotName];
                Int64 maximumDispatchedValue = projectionsForThisSlot
                    .Select(p => _checkpointTracker.GetCheckpoint(p))
                    .Max();

                //Create the dispatcher, a dispatcher is an object that is able to dispatch events to projections.
                var dispatcher = new RebuildProjectionSlotDispatcher(Logger, slotName, _config, projectionsForThisSlot, maximumDispatchedValue);
                JarvisFrameworkKernelMetricsHelper.SetCheckpointCountToDispatch(slotName, () => dispatcher.CheckpointToDispatch);

                //For this slot we need to know the bucket that can dispatch it, the rule is simple, find the bucket that contains
                //this exact slot name, and if you are unable to find that, find the bucket that has * (default bucket.)
                var slotBucket = _config.BucketInfo.SingleOrDefault(b =>
                    b.Slots.Any(s => s.Equals(slotName, StringComparison.OrdinalIgnoreCase))) ??
                    _config.BucketInfo.Single(b => b.Slots[0] == "*");
                var consumerList = _consumers[slotBucket];

                //once you find the consumerList (related to owner bucket) add this dispatcher to allow that bucket to
                //dispatch the events to the right slots.
                consumerList.Add(dispatcher);
            }

            //Creates TPL chain and start polling everything on the consumer, we have a consumer for each bucket, each
            //consumer will start a single poller of commit.
            foreach (var consumer in _consumers)
            {
                var bucketInfo = String.Join(",", consumer.Key.Slots);

                //We could have some bucket with NO slot to rebuild, those bucket will be marked as finished (zero commit to dispatch)
                if (consumer.Value.Count == 0)
                {
                    Logger.InfoFormat("Bucket {0} has no active slot, and will be ignored!", bucketInfo);
                    _status.BucketDone(bucketInfo, 0, 0, 0);
                    continue;
                }

                var consumerBufferOptions = new DataflowBlockOptions();
                consumerBufferOptions.BoundedCapacity = consumer.Key.BufferSize;

                var _buffer = new BufferBlock<DomainEvent>(consumerBufferOptions);

                ExecutionDataflowBlockOptions executionOption = new ExecutionDataflowBlockOptions();
                executionOption.BoundedCapacity = consumer.Key.BufferSize;

                //List of all dispatchers belonging to this bucket.
                var dispatcherList = consumer.Value;

                _projectionInspector.ResetHandledEvents();
            
                List<SlotGuaranteedDeliveryBroadcastBlock.SlotInfo<DomainEvent>> consumers =
                    new List<SlotGuaranteedDeliveryBroadcastBlock.SlotInfo<DomainEvent>>();

                foreach (var dispatcher in dispatcherList)
                {
                    ExecutionDataflowBlockOptions consumerOptions = new ExecutionDataflowBlockOptions();
                    consumerOptions.BoundedCapacity = consumer.Key.BufferSize;
                    var actionBlock = new ActionBlock<DomainEvent>(dispatcher.DispatchStartEventAsync, consumerOptions);
                    HashSet<Type> eventsOfThisSlot = new HashSet<Type>();
                    foreach (var projection in dispatcher.Projections)
                    {
                        var domainEventTypesHandledByThisProjection = _projectionInspector.InspectProjectionForEvents(projection.GetType());
                        foreach (var type in domainEventTypesHandledByThisProjection)
                        {
                            eventsOfThisSlot.Add(type);
                        }
                    }

                    SlotGuaranteedDeliveryBroadcastBlock.SlotInfo<DomainEvent> slotInfo =
                        new SlotGuaranteedDeliveryBroadcastBlock.SlotInfo<DomainEvent>(
                            actionBlock,
                            dispatcher.SlotName,
                            eventsOfThisSlot);
                    consumers.Add(slotInfo);
                    JarvisFrameworkKernelMetricsHelper.CreateMeterForRebuildDispatcherBuffer(dispatcher.SlotName, () => actionBlock.InputCount);
                }
                var allTypeHandledStringList = _projectionInspector
                    .EventHandled
                    .Select(t => VersionInfoAttributeHelper.GetEventName(t))
                    .ToList();

                var broadcaster = SlotGuaranteedDeliveryBroadcastBlock.CreateForStandardEvents(consumers, bucketInfo, consumer.Key.BufferSize);
                _buffer.LinkTo(broadcaster, new DataflowLinkOptions() { PropagateCompletion = true });

                JarvisFrameworkKernelMetricsHelper.CreateGaugeForRebuildFirstBuffer(bucketInfo, () => _buffer.Count);
                JarvisFrameworkKernelMetricsHelper.CreateGaugeForRebuildBucketDBroadcasterBuffer(bucketInfo, () => broadcaster.InputCount);

                JarvisFrameworkKernelMetricsHelper.CreateMeterForRebuildEventCompleted(bucketInfo);

                //fire each bucket in own thread
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Factory.StartNew(() => StartPoll(
                    _buffer,
                    broadcaster,
                    bucketInfo,
                    dispatcherList,
                    allTypeHandledStringList,
                    consumers));

#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }

            JarvisFrameworkKernelMetricsHelper.SetProjectionEngineCurrentDispatchCount(() => RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);
            return _status;
        }

        private Task EnsureIndexAsync()
        {
            return _commitCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<MongoChunk>(
                    Builders<MongoChunk>.IndexKeys
                        .Ascending("Payload.Events._t")
                        .Ascending("_id"),
                    new CreateIndexOptions()
                    {
                        Background = true,
                        Name = "RebuildV2_polling"
                    }));
        }

        private String _pollError = null;

        private async Task StartPoll(
            BufferBlock<DomainEvent> buffer,
            ActionBlock<DomainEvent> broadcaster,
            String bucketInfo,
            List<RebuildProjectionSlotDispatcher> dispatchers,
            List<String> allEventTypesHandledByAllSlots,
            List<SlotGuaranteedDeliveryBroadcastBlock.SlotInfo<DomainEvent>> consumers)
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

            Int64 lastCheckpointTokenDispatched = 0;
            //Grab the total count of the commit contained in the whole repository
            Logger.Info("Calculating total of Chunks in NStore");
            Int64 totalNumberOfChunks = _commitCollection.AsQueryable().Count();
            Logger.InfoFormat("Total of Chunks in NStore_ {0}", totalNumberOfChunks);
            Int64 dispatchedChunks = 0;

            //this is the main cycle that continue to poll events and send to the tpl buffer
            Boolean done = false;
            Logger.InfoFormat("Started polling for bucket {0}", bucketInfo);
            while (!done)
            {
                try
                {
                    IFindFluent<MongoChunk, MongoChunk> filter = _commitCollection
                        .Find(
                                //Greater than last dispatched, less than maximum dispatched and one of the events that are elaborated by at least one of the projection
                                Builders<MongoChunk>.Filter.And(
                                    Builders<MongoChunk>.Filter.Gt("_id", lastCheckpointTokenDispatched) //or checkpoint token is greater than last dispatched.
                                    , Builders<MongoChunk>.Filter.In("Payload.Events._t", allEventTypesHandledByAllSlots)
                             )
                        );

                    Logger.InfoFormat($"polled rebuild query {filter}");
                    var query = filter
                        .Sort(Builders<MongoChunk>.Sort.Ascending("_id"));
                    await query.ForEachAsync(Dispatch).ConfigureAwait(false);

                    async Task Dispatch(IChunk chunk)
                    {
                        dispatchedChunks++;
                        _pollError = null;
                        _commitEnhancer.Enhance(chunk);
                        if (Logger.IsDebugEnabled) Logger.DebugFormat("TPL queued chunk {0} for bucket {1}", chunk.Position, bucketInfo);
                        //rehydrate the event before dispatching
                        if (chunk.Payload is Changeset cs)
                        {
                            StaticUpcaster.UpcastChangeset(cs);

                            foreach (var @event in cs.Events.OfType<DomainEvent>())
                            {
                                await buffer.SendAsync(@event).ConfigureAwait(false);
                            }
                        }
                        lastCheckpointTokenDispatched = chunk.Position;
                    }

                    //Send marker unwindws domain event to signal that rebuild is finished
                    await buffer.SendAsync(LastEventMarker.Instance).ConfigureAwait(false);
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
                                dispatcher.LastCheckpointDispatched,
                                someEventDispatched: true //we are in rebuild, this is the last flush, surely we dispatched something.
                            ).ConfigureAwait(false);

                            dispatcherWaitingToFinish.Remove(dispatcher);
                            Logger.InfoFormat("Rebuild ended for slot {0}", dispatcher.SlotName);
                        }

                        i--;
                    }
                    if (dispatcherWaitingToFinish.Count > 0) Thread.Sleep(2000);
                }
                sw.Stop();
                Logger.InfoFormat("Bucket {0} finished dispathing all events for slots: {1}", bucketInfo, dispatchers.Select(d => d.SlotName).Aggregate((s1, s2) => s1 + ", " + s2));
                _status.BucketDone(bucketInfo, dispatchedChunks, sw.ElapsedMilliseconds, totalNumberOfChunks);
            }
            catch (Exception ex)
            {
                Logger.FatalFormat(ex, "Error during rebuild finish {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Simply setup Checkpoint tracker and then drop and setup every projection so they are
        /// ready to re-project all events.
        /// </summary>
        /// <returns></returns>
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

        private IMongoCollection<BsonDocument> GetMongoCommitsCollection()
        {
            var url = new MongoUrl(_config.EventStoreConnectionString);
            var client = url.CreateClient(false);
            var db = client.GetDatabase(url.DatabaseName);
            return db.GetCollection<BsonDocument>(EventStoreFactory.PartitionCollectionName);
        }
    }
}

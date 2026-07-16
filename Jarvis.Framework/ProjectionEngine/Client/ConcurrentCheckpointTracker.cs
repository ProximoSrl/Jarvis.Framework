using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Driver;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <inheritdoc />
    public class ConcurrentCheckpointTracker : IConcurrentCheckpointTracker
    {
        private readonly IMongoCollection<Checkpoint> _checkpoints;
        private readonly DurableCheckpointWriteBatcher _durableCheckpointWriteBatcher;

        private ConcurrentDictionary<string, Int64> _checkpointTracker;

        /// <summary>
        /// This contains last time a slot was flushed, this is needed for
        /// the optimization not to flush checkpoint every dispatched event.
        /// </summary>
        private ConcurrentDictionary<string, FlushSlotInfo> _lastFlushForEachSlot;

        /// <summary>
        /// Used for Metrics.NET.
        /// </summary>
        private ConcurrentDictionary<string, Int64> _checkpointSlotTracker;

        /// <summary>
        /// Key is the id of the projection and value is the name of the slot
        /// projection belongs to.
        /// </summary>
        private ConcurrentDictionary<String, String> _projectionToSlot;

        /// <summary>
        /// Used to avoid writing on Current during rebuild, for each slot
        /// it stores if it is in rebuild or not.
        /// </summary>
        private ConcurrentDictionary<String, Boolean> _slotRebuildTracker;

        /// <summary>
        /// Useful for Metrics.Net, I need to keep track of maximum value of
        /// dispatched commit for each slot. For the rebuild to be considered
        /// finished, all projection needs to reach this number,
        /// </summary>
        private Int64 _higherCheckpointToDispatchInRebuild;

        public ILogger Logger { get; set; }

        private List<string> _checkpointErrors;

        /// <summary>
        /// This is the time after the checkpoint in database is updated even if
        /// the slot did not dispatched any events for processed commit. We need to
        /// flush every X seconds because if a slot does not process any events, the
        /// checkpoint will never be updated and when the projection service restart
        /// it will re-dispatch lots of unnecessary events.
        /// </summary>
        /// <remarks>A negative or zero value actively disable deferred flush returning
        /// the checkpoint to the standard / classic operation mode.</remarks>
        public Int32 FlushNotDispatchedTimeoutInSeconds { get; set; } = 60;

        private List<Checkpoint> _initialCollectionContent;

        private bool DeferredFlushEnabled => FlushNotDispatchedTimeoutInSeconds > 0;

        public ConcurrentCheckpointTracker(
            IMongoDatabase db,
            int flushNotDispatchedTimeoutInSeconds)
            : this(
                db?.GetCollection<Checkpoint>("checkpoints") ?? throw new ArgumentNullException(nameof(db)),
                flushNotDispatchedTimeoutInSeconds)
        {
            _checkpoints.Indexes.CreateOne(
                    new CreateIndexModel<Checkpoint>(
                        Builders<Checkpoint>.IndexKeys.Ascending(x => x.Slot)
                    )
                );
        }

        internal ConcurrentCheckpointTracker(
            IMongoCollection<Checkpoint> checkpoints,
            int flushNotDispatchedTimeoutInSeconds)
            : this(
                checkpoints,
                flushNotDispatchedTimeoutInSeconds,
                TimeSpan.FromMilliseconds(1))
        {
        }

        internal ConcurrentCheckpointTracker(
            IMongoCollection<Checkpoint> checkpoints,
            int flushNotDispatchedTimeoutInSeconds,
            TimeSpan checkpointCollectionWindow)
        {
            _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
            _durableCheckpointWriteBatcher = new DurableCheckpointWriteBatcher(
                checkpointCollectionWindow,
                ExecuteCheckpointBatchAsync);
            Logger = NullLogger.Instance;
            Clear();
            FlushNotDispatchedTimeoutInSeconds = flushNotDispatchedTimeoutInSeconds;
        }

        private void Clear()
        {
            _checkpointTracker = new ConcurrentDictionary<string, Int64>();
            _lastFlushForEachSlot = new ConcurrentDictionary<string, FlushSlotInfo>();
            _checkpointSlotTracker = new ConcurrentDictionary<string, Int64>();
            _projectionToSlot = new ConcurrentDictionary<String, String>();
            _slotRebuildTracker = new ConcurrentDictionary<string, bool>();
            _checkpointErrors = new List<string>();
            _higherCheckpointToDispatchInRebuild = 0;
        }

        private void Add(IProjection projection, Int64 defaultValue)
        {
            var id = projection.Info.CommonName;
            var projectionSignature = projection.Info.Signature;
            var projectionSlotName = projection.Info.SlotName;

            //use the initial collection content to avoid reading the collection for each projection (limit mongodb calls)
            var checkPoint = _initialCollectionContent.SingleOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ??
                new Checkpoint(id, defaultValue, projectionSignature);

            //Check if some projection is changed and rebuild is not active
            if (
                checkPoint.Signature != projectionSignature
                && !RebuildSettings.ShouldRebuild)
            {
                _checkpointErrors.Add(String.Format("Projection {0} [slot {1}] has signature {2} but checkpoint on database has signature {3}.\n REBUILD NEEDED",
                    id, projectionSlotName, projectionSignature, checkPoint.Signature));
            }
            else
            {
                checkPoint.Signature = projectionSignature;
            }
            checkPoint.Slot = projectionSlotName;
            checkPoint.Active = true;
            _checkpoints.Save(checkPoint, checkPoint.Id);
            _checkpointTracker[id] = checkPoint.Value;
            _projectionToSlot[id] = checkPoint.Slot;
        }

        public void SetUp(IProjection[] projections, int version, Boolean setupMetrics)
        {
            Checkpoint versionInfo = _checkpoints.FindOneById("VERSION")
                ?? new Checkpoint("VERSION", 0, null);

            int currentVersion = (Int32)versionInfo.Value;

            Int64 projectionStartFormCheckpointValue = 0L;
            if (currentVersion == 0)
            {
                var eventsVersion = _checkpoints.FindOneById(EventStoreFactory.PartitionCollectionName);
                if (eventsVersion != null)
                {
                    projectionStartFormCheckpointValue = eventsVersion.Value;
                }
            }

            //set all projection to active = false
            _checkpoints.UpdateMany(
                Builders<Checkpoint>.Filter.Ne(c => c.Slot, null),
                Builders<Checkpoint>.Update.Set(c => c.Active, false)
            );

            _initialCollectionContent = _checkpoints.FindAll().ToList();
            foreach (var projection in projections)
            {
                Add(projection, projectionStartFormCheckpointValue);
            }

            // mark db
            if (version > currentVersion)
            {
                versionInfo.Value = version;
                _checkpoints.Save(versionInfo, versionInfo.Id);
            }

            foreach (var slot in projections.Select(p => p.Info.SlotName).Distinct())
            {
                var slotName = slot;
                if (setupMetrics)
                {
                    JarvisFrameworkKernelMetricsHelper.SetCheckpointCountToDispatch(slot, () => GetCheckpointCount(slotName));
                }

                _checkpointSlotTracker[slot] = 0;
                _slotRebuildTracker[slot] = false;
            }

            if (setupMetrics)
            {
                JarvisFrameworkKernelMetricsHelper.SetCheckpointCountToDispatch("", GetCheckpointMaxCount);
            }
        }

        private double GetCheckpointMaxCount()
        {
            return _checkpointSlotTracker.Max(k => k.Value);
        }

        private double GetCheckpointCount(string slotName)
        {
            return _checkpointSlotTracker[slotName];
        }

        public void RebuildStarted(IProjection projection, Int64 lastCommit)
        {
            // Rebuild changes Current to null, which intentionally opens the
            // monotonic filter to the next rebuild write. Fence the cold-path
            // queue first so an older pre-rebuild token cannot use that null arm;
            // this host has no synchronization context, so sync-over-async is safe.
            _durableCheckpointWriteBatcher.DrainAsync().GetAwaiter().GetResult();

            var projectionName = projection.Info.CommonName;
            _checkpoints.UpdateOne(
                Builders<Checkpoint>.Filter.Eq("_id", projectionName),
                Builders<Checkpoint>.Update.Set(x => x.RebuildStart, DateTime.UtcNow)
                                    .Set(x => x.RebuildStop, null)
                                    .Set(x => x.RebuildTotalSeconds, 0)
                                    .Set(x => x.RebuildActualSeconds, 0)
                                    .Set(x => x.Current, null)
                                    .Set(x => x.Events, 0)
                                    .Set(x => x.Details, null)
            );
            //Check if some commit was deleted and lastCommitId in Eventstore 
            //is lower than the latest dispatched commit.
            var trackerLastValue = _checkpointTracker[projection.Info.CommonName];
            if (trackerLastValue > 0)
            {
                //new projection, it has no dispatched checkpoint.
                _slotRebuildTracker[_projectionToSlot[projectionName]] = true;
            }
            if (lastCommit < trackerLastValue)
            {
                _checkpointTracker[projection.Info.CommonName] = lastCommit;
                _checkpoints.UpdateOne(
                   Builders<Checkpoint>.Filter.Eq("_id", projectionName),
                   Builders<Checkpoint>.Update.Set(x => x.Value, lastCommit));
            }
            _higherCheckpointToDispatchInRebuild = Math.Max(
                _higherCheckpointToDispatchInRebuild,
                Math.Min(trackerLastValue, lastCommit));
        }

        public void RebuildEnded(IProjection projection, ProjectionMetrics.Meter meter)
        {
            var projectionName = projection.Info.CommonName;
            _slotRebuildTracker[_projectionToSlot[projectionName]] = false;
            var checkpoint = _checkpoints.FindOneById(projectionName);
            checkpoint.RebuildStop = DateTime.UtcNow;
            if (checkpoint.RebuildStop.HasValue && checkpoint.RebuildStart.HasValue)
            {
                checkpoint.RebuildTotalSeconds = (long)(checkpoint.RebuildStop - checkpoint.RebuildStart).Value.TotalSeconds;
            }

            checkpoint.Events = meter.TotalEvents;
            checkpoint.RebuildActualSeconds = (double)meter.TotalElapsed / TimeSpan.TicksPerSecond;
            checkpoint.Details = meter;
            checkpoint.Signature = projection.Info.Signature;
            _checkpoints.Save(checkpoint, checkpoint.Id);
        }

        public async Task UpdateSlotAndSetCheckpointAsync(
            string slotName,
            IEnumerable<String> projectionNameList,
            Int64 valueCheckpointToken,
            bool someEventDispatched)
        {
            //we do not want to update mongodb if the event was not dispatched on the slot, this
            //because if we re-dispatch again (in case of immediate shutdown) we do not have side effects
            //Clearly if we do not have enabled deferred flush, we should always update mongo
            bool shouldUpdateMongodb = someEventDispatched || !DeferredFlushEnabled;

            //if we determined that we should not update mongo, we need to check if some time passed
            //because we want to flush every X seconds even if the slot
            if (!shouldUpdateMongodb)
            {
                if (!_lastFlushForEachSlot.TryGetValue(slotName, out var lastFlushInfo))
                {
                    //ok we never flushed the slot, we can simply assume that time tracking starts from now.
                    lastFlushInfo = new FlushSlotInfo()
                    {
                        LastFlush = DateTime.UtcNow,
                        PendingFlush = true,
                    };
                    _lastFlushForEachSlot[slotName] = lastFlushInfo;
                }

                var checkpointToWrite = valueCheckpointToken;
                lock (lastFlushInfo.Gate)
                {
                    //flush is pending and we store actual checkpoint token.
                    lastFlushInfo.PendingFlush = true;
                    // A deferred flush may observe several tokens for the same slot;
                    // retain the highest one so it never enqueues an older checkpoint.
                    lastFlushInfo.ActualCheckpoint = Math.Max(lastFlushInfo.ActualCheckpoint, valueCheckpointToken);

                    //ok now we have last flush time, we need to determine if we really want to flush because
                    //too much time passed. Dispatching in a certain amount of time is a safe assumption.
                    if (DateTime.UtcNow.Subtract(lastFlushInfo.LastFlush).TotalSeconds > FlushNotDispatchedTimeoutInSeconds) //time elapsed
                    {
                        shouldUpdateMongodb = true;
                        lastFlushInfo.LastFlush = DateTime.UtcNow;
                        // Keep the maximum collected token when the timed path writes immediately.
                        checkpointToWrite = lastFlushInfo.ActualCheckpoint;
                        //we are going to flush pending flush become false.
                        lastFlushInfo.PendingFlush = false;
                    }
                }

                valueCheckpointToken = checkpointToWrite;
            }

            //Update slot only if it is needed, this will greatly reduce the concurrency and resource lock on the checkpoint collection.
            //Distinct slots completing within the small collection window share one MongoDB bulk write. Requests for
            //the same slot remain separate and ordered in the durable checkpoint coordinator.
            if (shouldUpdateMongodb)
            {
                await _durableCheckpointWriteBatcher
                    .EnqueueAsync(slotName, valueCheckpointToken)
                    .ConfigureAwait(false);
            }
            foreach (var projectionName in projectionNameList)
            {
                _checkpointTracker.AddOrUpdate(
                    projectionName,
                    _ => valueCheckpointToken,
                    (_, current) => Math.Max(current, valueCheckpointToken));
            }
        }

        /// <summary>
        /// Persists a distinct-slot checkpoint batch using the asynchronous MongoDB driver API.
        /// </summary>
        /// <param name="writes">One durable checkpoint request for each distinct slot in the batch.</param>
        /// <returns></returns>
        private async Task<IReadOnlyDictionary<int, Exception>> ExecuteCheckpointBatchAsync(
            IReadOnlyList<DurableCheckpointWriteBatcher.DurableCheckpointWrite> writes)
        {
            var models = CreateCheckpointWriteModels(writes);
            try
            {
                var result = await _checkpoints.BulkWriteAsync(
                    models,
                    new BulkWriteOptions { IsOrdered = false },
                    CancellationToken.None).ConfigureAwait(false);

                EnsureAcknowledged(result, writes.Count);
                return DurableCheckpointWriteBatcher.EmptyFailures;
            }
            catch (MongoBulkWriteException<Checkpoint> ex) when (CanIdentifySuccessfulRequests(ex, writes.Count))
            {
                return ex.WriteErrors.ToDictionary(error => error.Index, _ => (Exception)ex);
            }
        }

        private static List<WriteModel<Checkpoint>> CreateCheckpointWriteModels(
            IReadOnlyList<DurableCheckpointWriteBatcher.DurableCheckpointWrite> writes)
        {
            // There is one update model for each distinct slot head. The
            // monotonic filter makes retries after an ambiguous bulk safe: an
            // older token can be acknowledged as a no-op, but cannot regress it.
            return writes
                .Select(write => (WriteModel<Checkpoint>)new UpdateManyModel<Checkpoint>(
                    CreateMonotonicSlotFilter(write.SlotName, write.CheckpointToken),
                    Builders<Checkpoint>.Update
                        .Set(checkpoint => checkpoint.Current, write.CheckpointToken)
                        .Set(checkpoint => checkpoint.Value, write.CheckpointToken)))
                .ToList();
        }

        private static FilterDefinition<Checkpoint> CreateMonotonicSlotFilter(
            string slotName,
            long valueCheckpointToken)
        {
            return Builders<Checkpoint>.Filter.Eq(checkpoint => checkpoint.Slot, slotName) &
                (
                    Builders<Checkpoint>.Filter.Lt(checkpoint => checkpoint.Current, valueCheckpointToken) |
                    Builders<Checkpoint>.Filter.Eq(checkpoint => checkpoint.Current, null)
                );
        }

        private static void EnsureAcknowledged(BulkWriteResult<Checkpoint> result, int expectedRequestCount)
        {
            if (!result.IsAcknowledged || result.RequestCount != expectedRequestCount)
            {
                throw new InvalidOperationException(
                    $"MongoDB did not acknowledge all checkpoint writes. Expected {expectedRequestCount}, acknowledged {result.RequestCount}.");
            }
        }

        private static bool CanIdentifySuccessfulRequests(
            MongoBulkWriteException<Checkpoint> exception,
            int expectedRequestCount)
        {
            return CanIdentifySuccessfulRequests(
                exception.Result?.IsAcknowledged == true,
                exception.Result?.RequestCount ?? -1,
                exception.WriteErrors.Select(error => error.Index).ToArray(),
                exception.WriteConcernError != null,
                exception.UnprocessedRequests.Count,
                expectedRequestCount);
        }

        internal static bool CanIdentifySuccessfulRequests(
            bool resultIsAcknowledged,
            int resultRequestCount,
            IReadOnlyCollection<int> indexedErrorIndexes,
            bool hasWriteConcernError,
            int unprocessedRequestCount,
            int expectedRequestCount)
        {
            // Unordered bulk writes can partially apply before MongoDB reports an
            // error. Split the failure only when the driver proves that every
            // request was processed and each error maps to one valid index;
            // otherwise the outcome is ambiguous and the whole batch must fail.
            return !hasWriteConcernError &&
                resultIsAcknowledged &&
                resultRequestCount == expectedRequestCount &&
                unprocessedRequestCount == 0 &&
                indexedErrorIndexes.Count > 0 &&
                indexedErrorIndexes.All(index => index >= 0 && index < expectedRequestCount) &&
                indexedErrorIndexes.Distinct().Count() == indexedErrorIndexes.Count;
        }

        public async Task UpdateProjectionCheckpointAsync(
            string projectionName,
            Int64 checkpointToken)
        {
            await _checkpoints.UpdateOneAsync(
                       Builders<Checkpoint>.Filter.Eq(_ => _.Id, projectionName),
                       Builders<Checkpoint>.Update
                        .Set(_ => _.Current, checkpointToken)
                        .Set(_ => _.Value, checkpointToken)
                ).ConfigureAwait(false);

            //Now update in-memory cache
            _checkpointTracker.AddOrUpdate(projectionName, _ => checkpointToken, (_, __) => checkpointToken);
        }

        public Checkpoint GetFullCheckpoint(IProjection projection)
        {
            var checkpoint = _checkpoints.FindOneById(projection.Info.CommonName);
            if (checkpoint == null)
            {
                // we do not need to create a null checkpoint the caller can know what to do with a null checkpoint
                return null;
            }

            return checkpoint;
        }

        public Int64 GetCheckpoint(IProjection projection)
        {
            var id = projection.Info.CommonName;
            if (_checkpointTracker.TryGetValue(id, out var value))
            {
                return value;
            }

            return 0;
        }

        public CheckPointReplayStatus GetCheckpointStatus(string projectionName, Int64 checkpoint)
        {
            Int64 lastDispatched = _checkpointTracker[projectionName];

            var currentCheckpointValue = checkpoint;
            long lastDispatchedValue = lastDispatched;

            //This is the last checkpoint in rebuild only if the continuous rebuild is not set.
            bool isLast = currentCheckpointValue == lastDispatchedValue && !RebuildSettings.ContinuousRebuild;
            //is replay is always true if we are in Continuous rebuild.
            bool isReplay = currentCheckpointValue <= lastDispatchedValue || RebuildSettings.ContinuousRebuild;
            _checkpointSlotTracker[_projectionToSlot[projectionName]] = _higherCheckpointToDispatchInRebuild - currentCheckpointValue;
            return new CheckPointReplayStatus(isLast, isReplay);
        }

        public async Task FlushCheckpointAsync()
        {
            //Fence writes from callers that have already completed projection side effects before
            //adding deferred checkpoints, then drain the deferred writes as part of the same flush.
            await _durableCheckpointWriteBatcher.DrainAsync().ConfigureAwait(false);

            if (!DeferredFlushEnabled)
            {
                return;
            }

            try
            {
                // The first drain fences writes already queued. Deferred writes
                // are then enqueued together and the second drain waits for all
                // of those newly queued requests to be acknowledged.
                var pendingFlushes = new List<(string SlotName, long CheckpointToken, FlushSlotInfo SlotInfo)>();
                foreach (var flush in _lastFlushForEachSlot)
                {
                    lock (flush.Value.Gate)
                    {
                        if (!flush.Value.PendingFlush)
                        {
                            continue;
                        }

                        // Snapshot and clear before enqueueing. A dispatch that arrives
                        // after this lock is released sets PendingFlush again, so it is
                        // picked up by the next flush; clearing after enqueue could erase
                        // that newer token.
                        pendingFlushes.Add((
                            flush.Key,
                            flush.Value.ActualCheckpoint,
                            flush.Value));
                        flush.Value.PendingFlush = false;
                    }
                }

                var flushTasks = pendingFlushes.Select(FlushDeferredCheckpointAsync);

                await Task.WhenAll(flushTasks).ConfigureAwait(false);
                await _durableCheckpointWriteBatcher.DrainAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Error during flush of checkpoints");
            }
        }

        private async Task FlushDeferredCheckpointAsync(
            (string SlotName, long CheckpointToken, FlushSlotInfo SlotInfo) flush)
        {
            try
            {
                await _durableCheckpointWriteBatcher
                    .EnqueueAsync(flush.SlotName, flush.CheckpointToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                lock (flush.SlotInfo.Gate)
                {
                    // Restore only this failed request. A dispatch racing with the
                    // flush may already have supplied a newer token; retain it while
                    // ensuring the slot remains eligible for the next flush.
                    flush.SlotInfo.ActualCheckpoint = Math.Max(
                        flush.SlotInfo.ActualCheckpoint,
                        flush.CheckpointToken);
                    flush.SlotInfo.PendingFlush = true;
                }

                throw;
            }
        }

        private class FlushSlotInfo
        {
            public readonly object Gate = new();

            public DateTime LastFlush { get; set; }

            public Boolean PendingFlush { get; set; }

            public long ActualCheckpoint { get; set; }
        }
    }
}

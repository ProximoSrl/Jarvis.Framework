using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Driver;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <inheritdoc />
    /// <remarks>
    /// The tracker never writes to MongoDB on the hot path. Every checkpoint update goes to a local,
    /// crash-safe medium (<see cref="FileSystemCheckpointDurableStore"/>) and to the in-memory value;
    /// a single timer periodically pushes the in-memory state to MongoDB. On startup the tracker
    /// reconciles MongoDB with the local medium, keeping whichever is greater, so a crash between two
    /// flushes never replays already-dispatched side effects. <see cref="FlushCheckpointAsync"/> forces
    /// a flush on demand (it is also called on graceful shutdown).
    /// </remarks>
    public class ConcurrentCheckpointTracker : IConcurrentCheckpointTracker, IConcurrentCheckpointStatusChecker, IDisposable
    {
        private readonly IMongoCollection<Checkpoint> _checkpoints;

        /// <summary>
        /// Local crash-safe medium that holds the last dispatched checkpoint of each slot. Injected
        /// (the default is a <see cref="FileSystemCheckpointDurableStore"/>); always present.
        /// </summary>
        private readonly ICheckpointDurableStore _durableStore;

        /// <summary>
        /// Periodic MongoDB flush timer. Null when the automatic timer is disabled (a non-positive
        /// interval, or disabled for tests that drive <see cref="FlushCheckpointAsync"/> manually).
        /// </summary>
        private readonly System.Timers.Timer _flushTimer;

        /// <summary>
        /// Serializes every MongoDB flush (timer, explicit and shutdown) and fences a rebuild
        /// reset so that only one thread ever writes checkpoints to MongoDB at a time.
        /// </summary>
        private readonly SemaphoreSlim _flushGate = new SemaphoreSlim(1, 1);

        private ConcurrentDictionary<string, Int64> _checkpointTracker;

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

        private int _flushNotDispatchedTimeoutInSeconds = 60;

        private volatile bool _disposed;

        /// <summary>
        /// Interval, in seconds, of the timer that pushes the in-memory checkpoint state to MongoDB.
        /// Because MongoDB is not updated on every dispatched commit (that is the whole point of the
        /// local durable medium), this timer guarantees the durable MongoDB copy is refreshed
        /// periodically.
        /// </summary>
        /// <remarks>A negative or zero value disables the automatic timer: checkpoints are then
        /// persisted to MongoDB only on an explicit <see cref="FlushCheckpointAsync"/> or on
        /// shutdown. It does NOT change how the hot path works: updates always go to the local
        /// durable medium and the in-memory value.</remarks>
        public Int32 FlushNotDispatchedTimeoutInSeconds
        {
            get => _flushNotDispatchedTimeoutInSeconds;
            set
            {
                _flushNotDispatchedTimeoutInSeconds = value;
                if (_flushTimer != null && value > 0)
                {
                    _flushTimer.Interval = value * 1000d;
                }
            }
        }

        private List<Checkpoint> _initialCollectionContent;

        public ConcurrentCheckpointTracker(
            IMongoDatabase db,
            int flushNotDispatchedTimeoutInSeconds)
            : this(
                db?.GetCollection<Checkpoint>("checkpoints") ?? throw new ArgumentNullException(nameof(db)),
                flushNotDispatchedTimeoutInSeconds)
        {
        }

        /// <summary>
        /// Builds the tracker with the default file-system durable store, whose folder is derived from a
        /// per-database seed (see <see cref="GetStoreBaseDirectory"/>) so that two projection services
        /// pointing at different databases on the same host never share the same slot file.
        /// </summary>
        private ConcurrentCheckpointTracker(
            IMongoCollection<Checkpoint> checkpoints,
            int flushNotDispatchedTimeoutInSeconds)
            : this(
                checkpoints,
                flushNotDispatchedTimeoutInSeconds,
                new FileSystemCheckpointDurableStore(GetStoreBaseDirectory(checkpoints)))
        {
            checkpoints.Indexes.CreateOne(
                    new CreateIndexModel<Checkpoint>(
                        Builders<Checkpoint>.IndexKeys.Ascending(x => x.Slot)
                    )
                );
        }

        internal ConcurrentCheckpointTracker(
            IMongoCollection<Checkpoint> checkpoints,
            int flushNotDispatchedTimeoutInSeconds,
            ICheckpointDurableStore durableStore)
        {
            _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
            _durableStore = durableStore ?? throw new ArgumentNullException(nameof(durableStore));
            Logger = NullLogger.Instance;
            _flushNotDispatchedTimeoutInSeconds = flushNotDispatchedTimeoutInSeconds;
            Clear();

            if (_flushNotDispatchedTimeoutInSeconds > 0)
            {
                _flushTimer = new System.Timers.Timer(_flushNotDispatchedTimeoutInSeconds * 1000d)
                {
                    AutoReset = true,
                };
                _flushTimer.Elapsed += OnFlushTimerElapsed;
                _flushTimer.Start();
            }
        }

        /// <summary>
        /// Well-known id of the document, stored in the checkpoints collection, that seeds the local
        /// durable-store folder name. It is not a projection: it is left inactive
        /// (<see cref="Checkpoint.Active"/> == false) and has no slot, so every projection scan
        /// (status checker, slot status manager, metrics) ignores it.
        /// </summary>
        internal const string CheckpointStoreSeedId = "jarvis.framework.checkpoint.store.seed";

        /// <summary>
        /// Default local durable-store location: a per-database subfolder of "jarvis-framework-checkpoints"
        /// under the operating system temporary directory. The subfolder name is a stable seed persisted
        /// in the checkpoints collection, so the same database always resolves to the same folder across
        /// restarts, while two different databases never collide on the same slot files (which would let
        /// one recover the other's checkpoint through the startup max() reconciliation). Inject a custom
        /// <see cref="ICheckpointDurableStore"/> to override the location entirely.
        /// </summary>
        private static string GetStoreBaseDirectory(IMongoCollection<Checkpoint> checkpoints)
        {
            var seedDocument = checkpoints.FindOneById(CheckpointStoreSeedId);
            string seed = seedDocument?.Signature;
            if (String.IsNullOrWhiteSpace(seed))
            {
                seed = Guid.NewGuid().ToString("N");
                // Upsert without overwriting: SetOnInsert only writes the seed on the insert branch, so
                // if another process created it first we keep its value. Re-read to converge on the winner.
                checkpoints.UpdateOne(
                    Builders<Checkpoint>.Filter.Eq(c => c.Id, CheckpointStoreSeedId),
                    Builders<Checkpoint>.Update.SetOnInsert(c => c.Signature, seed),
                    new UpdateOptions { IsUpsert = true });
                seed = checkpoints.FindOneById(CheckpointStoreSeedId)?.Signature ?? seed;
            }

            return Path.Combine(Path.GetTempPath(), "jarvis-framework-checkpoints", seed);
        }

        private void Clear()
        {
            _checkpointTracker = new ConcurrentDictionary<string, Int64>();
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

            // Reconcile MongoDB (loaded above into the in-memory tracker by Add) with the local
            // durable medium: whichever is greater is the real last-dispatched checkpoint. This is
            // the crash-recovery step that lets MongoDB be updated at a slow pace.
            ReconcileWithDurableStore();

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

        private void ReconcileWithDurableStore()
        {
            foreach (var projectionToSlot in _projectionToSlot)
            {
                var durableValue = _durableStore.Read(projectionToSlot.Value);
                _checkpointTracker.AddOrUpdate(
                    projectionToSlot.Key,
                    _ => durableValue,
                    (_, current) => Math.Max(current, durableValue));
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
            // Rebuild changes Current to null, which intentionally opens the monotonic filter to
            // the next rebuild write. Fence the flush (timer / explicit) first so no older token
            // can be written to MongoDB after Current is reset; this host has no synchronization
            // context, so sync-over-async is safe.
            _flushGate.Wait();
            try
            {
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

                // Keep the local durable medium aligned with the (possibly lowered) checkpoint so
                // that a restart does not resurrect a pre-rebuild token via max() reconciliation.
                _durableStore.Reset(_projectionToSlot[projectionName], _checkpointTracker[projectionName]);
            }
            finally
            {
                _flushGate.Release();
            }
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

            // Current/Value in MongoDB may lag behind the value reached during the rebuild (only
            // the timer pushes them). Persist the authoritative in-memory value as part of this
            // same save so the rebuilt checkpoint is durable immediately.
            if (_checkpointTracker.TryGetValue(projectionName, out var inMemory))
            {
                checkpoint.Value = Math.Max(checkpoint.Value, inMemory);
                checkpoint.Current = Math.Max(checkpoint.Current ?? 0, inMemory);
            }

            _checkpoints.Save(checkpoint, checkpoint.Id);
        }

        public Task UpdateSlotAndSetCheckpointAsync(
            string slotName,
            IEnumerable<String> projectionNameList,
            Int64 valueCheckpointToken,
            bool someEventDispatched)
        {
            // Never touch MongoDB on the hot path. Record the checkpoint on the local crash-safe
            // medium and update the in-memory value. fsync only when the slot actually produced
            // side effects: those must never be replayed after a crash, while a non-dispatched
            // checkpoint can safely be lost and re-dispatched.
            _durableStore.Record(slotName, valueCheckpointToken, flushToDisk: someEventDispatched);
            UpdateInMemoryCheckpoint(projectionNameList, valueCheckpointToken);
            return Task.CompletedTask;
        }

        private void UpdateInMemoryCheckpoint(IEnumerable<String> projectionNameList, Int64 valueCheckpointToken)
        {
            foreach (var projectionName in projectionNameList)
            {
                _checkpointTracker.AddOrUpdate(
                    projectionName,
                    _ => valueCheckpointToken,
                    (_, current) => Math.Max(current, valueCheckpointToken));
            }
        }

        private static FilterDefinition<Checkpoint> CreateMonotonicSlotFilter(
            string slotName,
            long valueCheckpointToken)
        {
            // The monotonic filter makes the timed flush idempotent and safe against reordering:
            // an older token can be re-applied as a no-op but never regresses the slot.
            return Builders<Checkpoint>.Filter.Eq(checkpoint => checkpoint.Slot, slotName) &
                (
                    Builders<Checkpoint>.Filter.Lt(checkpoint => checkpoint.Current, valueCheckpointToken) |
                    Builders<Checkpoint>.Filter.Eq(checkpoint => checkpoint.Current, null)
                );
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

            // MongoDB lags behind the in-memory/durable value, so overlay the authoritative
            // in-memory checkpoint (already reconciled with the durable medium at SetUp) onto the
            // returned document. This is what makes the projection engine resume from the real
            // position instead of replaying commits.
            //
            // The one case we must not touch is an interrupted rebuild, signalled by
            // Current == null while Value > 0: leaving it as-is lets the engine detect the
            // interrupted rebuild and refuse to start. A null Current with Value == 0 is just a
            // projection that dispatched but never flushed yet, and MUST be recovered from the
            // durable value, otherwise its already-dispatched commits would be replayed.
            if (_checkpointTracker.TryGetValue(projection.Info.CommonName, out var inMemory))
            {
                bool interruptedRebuild = checkpoint.Current == null && checkpoint.Value > 0;
                if (!interruptedRebuild)
                {
                    long durableCurrent = checkpoint.Current ?? 0;
                    if (inMemory > durableCurrent)
                    {
                        checkpoint.Current = inMemory;
                    }
                    if (inMemory > checkpoint.Value)
                    {
                        checkpoint.Value = inMemory;
                    }
                }
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

        /// <summary>
        /// In-process check backed by the always-current in-memory checkpoint of every tracked
        /// (active) projection, so it never lags behind the periodic MongoDB flush and needs no forced
        /// flush to be accurate. Only projections registered in the last <see cref="SetUp"/> are
        /// considered, so a projection that is no longer active is correctly ignored.
        /// </summary>
        /// <remarks>Use <see cref="MongoDirectConcurrentCheckpointStatusChecker"/> instead when the
        /// caller is a DIFFERENT process (a monitor, an admin tool): it reads MongoDB directly because
        /// it has no access to this in-memory state, and therefore only observes values already flushed
        /// by the timer or an explicit <see cref="FlushCheckpointAsync"/>.</remarks>
        public bool IsCheckpointProjectedByAllProjection(Int64 checkpointToken)
        {
            foreach (var lastDispatched in _checkpointTracker.Values)
            {
                if (lastDispatched < checkpointToken)
                {
                    return false;
                }
            }

            return true;
        }

        public async Task FlushCheckpointAsync()
        {
            await _flushGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await FlushPendingToMongoAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Error during flush of checkpoints");
            }
            finally
            {
                _flushGate.Release();
            }
        }

        /// <summary>
        /// Pushes the current in-memory checkpoint of every slot to MongoDB in a single unordered
        /// bulk write. Must be called while holding <see cref="_flushGate"/>, guaranteeing a single
        /// writer. Because the write uses the monotonic filter it is idempotent, so flushing every
        /// slot on each tick (even unchanged ones) is safe and a failure simply retries next time.
        /// </summary>
        private Task FlushPendingToMongoAsync()
        {
            var slotTokens = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var projectionCheckpoint in _checkpointTracker)
            {
                if (!_projectionToSlot.TryGetValue(projectionCheckpoint.Key, out var slot))
                {
                    continue;
                }
                if (!slotTokens.TryGetValue(slot, out var current) || projectionCheckpoint.Value > current)
                {
                    slotTokens[slot] = projectionCheckpoint.Value;
                }
            }

            var models = slotTokens
                .Where(slotToken => slotToken.Value > 0)
                .Select(slotToken => (WriteModel<Checkpoint>)new UpdateManyModel<Checkpoint>(
                    CreateMonotonicSlotFilter(slotToken.Key, slotToken.Value),
                    Builders<Checkpoint>.Update
                        .Set(checkpoint => checkpoint.Current, slotToken.Value)
                        .Set(checkpoint => checkpoint.Value, slotToken.Value)))
                .ToList();

            if (models.Count == 0)
            {
                return Task.CompletedTask;
            }

            return _checkpoints.BulkWriteAsync(
                models,
                new BulkWriteOptions { IsOrdered = false },
                CancellationToken.None);
        }

        private void OnFlushTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            // Skip this tick if another flush (explicit or a rebuild fence) is already running:
            // a single-threaded, non-overlapping flush is exactly what we want.
            if (!_flushGate.Wait(0))
            {
                return;
            }

            try
            {
                FlushPendingToMongoAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Error during timed flush of checkpoints");
            }
            finally
            {
                _flushGate.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            if (_flushTimer != null)
            {
                _flushTimer.Stop();
                _flushTimer.Elapsed -= OnFlushTimerElapsed;
                _flushTimer.Dispose();
            }

            // Final flush so in-memory progress is persisted before we let go of the process.
            _flushGate.Wait();
            try
            {
                FlushPendingToMongoAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Error during final flush of checkpoints on dispose");
            }
            finally
            {
                _flushGate.Release();
            }

            _durableStore.Dispose();
            _flushGate.Dispose();
        }
    }
}

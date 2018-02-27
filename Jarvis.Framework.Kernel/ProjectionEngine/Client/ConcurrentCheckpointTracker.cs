using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.Support;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Kernel.Engine;
using System.Threading.Tasks;
using System.Threading;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public class ConcurrentCheckpointTracker : IConcurrentCheckpointTracker
    {
        public const string CollectionName = "checkpoints";

        /// <summary>
        /// This is the collection used to store all checkpoints.
        /// </summary>
        private readonly IMongoCollection<Checkpoint> _checkpoints;

        /// <summary>
        /// Dictionary with projection name as the key and last projected
        /// checkpoint as value.
        /// </summary>
        private ConcurrentDictionary<string, Int64> _checkpointTracker;

        /// <summary>
        /// Dictionary with slot name as the key and last projected checkpoint
        /// as value.
        /// Used for Metrics.NET.
        /// </summary>
        private ConcurrentDictionary<string, Int64> _checkpointSlotTracker;

		/// <summary>
		/// Key is the id of the projection and value is the name of the slot
		/// projection belongs to.
		/// </summary>
        private ConcurrentDictionary<String, String> _projectionToSlot;

        /// <summary>
        /// Useful for Metrics.Net, I need to keep track of maximum value of 
        /// dispatched commit for each slot. For the rebuild to be considered
        /// finished, all projection needs to reach this number,
        /// </summary>
        private Int64 _higherCheckpointToDispatchInRebuild;

        public ILogger Logger { get; set; }

        private List<string> _checkpointErrors;

        public ConcurrentCheckpointTracker(IMongoDatabase db)
        {
            _checkpoints = db.GetCollection<Checkpoint>(CollectionName);
            _checkpoints.Indexes.CreateOne(Builders<Checkpoint>.IndexKeys.Ascending(x => x.Slot));
            Logger = NullLogger.Instance;
            Clear();
        }

        private void Clear()
        {
            _checkpointTracker = new ConcurrentDictionary<string, Int64>();
            _checkpointSlotTracker = new ConcurrentDictionary<string, Int64>();
            _projectionToSlot = new ConcurrentDictionary<String, String>();
            _checkpointErrors = new List<string>();
            _higherCheckpointToDispatchInRebuild = 0;
        }

        private void Add(IProjection projection, Int64 defaultValue)
        {
            var id = projection.Info.CommonName;
            var projectionSignature = projection.Info.Signature;
            var projectionSlotName = projection.Info.SlotName;

            var checkPoint = _checkpoints.FindOneById(id) ??
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
                    projectionStartFormCheckpointValue = eventsVersion.Value;
            }

            //set all projection to active = false
            _checkpoints.UpdateMany(
                Builders<Checkpoint>.Filter.Ne(c => c.Slot, null),
                Builders<Checkpoint>.Update.Set(c => c.Active, false)
            );

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
                if (setupMetrics) MetricsHelper.SetCheckpointCountToDispatch(slot, () => GetCheckpointCount(slotName));
                var slotMinDispatched = _checkpoints.AsQueryable()
                    .OrderBy(_ => _.Current)
                    .Where(_ => _.Slot == slot)
                    .FirstOrDefault();
                _checkpointSlotTracker[slot] = slotMinDispatched?.Current ?? 0;
                _slotRebuildTracker[slot] = false;
            }

            if (setupMetrics) KernelMetricsHelper.SetCheckpointCountToDispatch("", GetCheckpointMaxCount);
        }

        private double GetCheckpointMaxCount()
        {
            return _checkpointSlotTracker.Select(k => k.Value).Max();
        }

        private double GetCheckpointCount(string slotName)
        {
            return _checkpointSlotTracker[slotName];
        }

        public void RebuildStarted(IProjection projection, Int64 lastCommit)
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

            //TODO, flus everything to the database.
        }

        /// <summary>
        /// This function is now used to  update the slot in memory, but we do not 
        /// write immediately in mongodb, to avoid doing unnecessary massive updates
        /// of checkpoint collection.
        /// </summary>
        /// <param name="slotName"></param>
        /// <param name="projectionNameList"></param>
        /// <param name="valueCheckpointToken"></param>
        /// <returns></returns>
        public Task UpdateSlotAndSetCheckpointAsync(
            string slotName,
			IEnumerable<String> projectionNameList,
			Int64 valueCheckpointToken)
        {
            foreach (var projectionName in projectionNameList)
            {
                _checkpointTracker.AddOrUpdate(projectionName, key => valueCheckpointToken, (key, currentValue) => valueCheckpointToken);
            }
            _checkpointSlotTracker[slotName] = valueCheckpointToken;
            return Task.CompletedTask;
        }

        private Int32 _isFlushing = 0;

        public async Task FlushCheckpointCollectionAsync()
        {
            try
            {
                if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) == 0)
                {
                    var grabAllSlotKeys = _checkpointSlotTracker.Keys.ToArray();
                    foreach (var key in grabAllSlotKeys)
                    {
                        var value = _checkpointSlotTracker[key];
                        if (value > 0) //if someone try to flush before the projection engine sets first commit dispatched, avoid flushing to db.
                        {
                            await _checkpoints.UpdateManyAsync(
                                    Builders<Checkpoint>.Filter.Eq("Slot", key),
                                    Builders<Checkpoint>.Update
                                    .Set(_ => _.Current, value)
                                    .Set(_ => _.Value, value)
                                ).ConfigureAwait(false);
                        }
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isFlushing, 0);
            }
        }

        public Int64 GetCurrent(IProjection projection)
        {
            var checkpoint = _checkpoints.FindOneById(projection.Info.CommonName);
            if (checkpoint == null)
                return 0;

            return checkpoint.Current ?? 0;
        }

		public Int64 GetCheckpoint(IProjection projection)
		{
			var id = projection.Info.CommonName;
			if (_checkpointTracker.ContainsKey(id))
				return _checkpointTracker[id];
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
            return new CheckPointReplayStatus(isLast, isReplay);
        }
    }
}

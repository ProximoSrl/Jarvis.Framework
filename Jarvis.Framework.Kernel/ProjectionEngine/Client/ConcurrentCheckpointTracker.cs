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

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public class ConcurrentCheckpointTracker : IConcurrentCheckpointTracker
    {
        readonly IMongoCollection<Checkpoint> _checkpoints;

        private ConcurrentDictionary<string, Int64> _checkpointTracker;

        private ConcurrentDictionary<string, Int64> _currentCheckpointTracker;

        /// <summary>
        /// Used for Metrics.NET.
        /// </summary>
        private ConcurrentDictionary<string, Int64> _checkpointSlotTracker;

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

        public ConcurrentCheckpointTracker(IMongoDatabase db)
        {
            _checkpoints = db.GetCollection<Checkpoint>("checkpoints");
            _checkpoints.Indexes.CreateOne(Builders<Checkpoint>.IndexKeys.Ascending(x => x.Slot));
            Logger = NullLogger.Instance;
            Clear();
        }

        public void Clear()
        {
            _checkpointTracker = new ConcurrentDictionary<string, Int64>();
            _checkpointSlotTracker = new ConcurrentDictionary<string, Int64>();
            _projectionToSlot = new ConcurrentDictionary<String, String>();
            _slotRebuildTracker = new ConcurrentDictionary<string, bool>();
            _currentCheckpointTracker = new ConcurrentDictionary<string, Int64>();
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
                _checkpointSlotTracker[slot] = 0;
                _slotRebuildTracker[slot] = false;
            }

            if (setupMetrics) MetricsHelper.SetCheckpointCountToDispatch("", GetCheckpointMaxCount);
        }

        private double GetCheckpointMaxCount()
        {
            return _checkpointSlotTracker.Select(k => k.Value).Max();
        }

        private double GetCheckpointCount(string slotName)
        {
            return _checkpointSlotTracker[slotName];
        }

        public Int64 GetMinCheckpoint()
        {
            return _currentCheckpointTracker.Values.Min();
        }

        public bool NeedsRebuild(IProjection projection)
        {
            return RebuildSettings.ShouldRebuild;
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
            var lastCommitIdLong = lastCommit;
            if (trackerLastValue > 0)
            {
                //new projection, it has no dispatched checkpoint.
                _slotRebuildTracker[_projectionToSlot[projectionName]] = true;
            }
            if (lastCommitIdLong < trackerLastValue)
            {
                _checkpointTracker[projection.Info.CommonName] = lastCommit;
                _checkpoints.UpdateOne(
                   Builders<Checkpoint>.Filter.Eq("_id", projectionName),
                   Builders<Checkpoint>.Update.Set(x => x.Value, lastCommit));
            }
            _higherCheckpointToDispatchInRebuild = Math.Max(
                _higherCheckpointToDispatchInRebuild,
                Math.Min(trackerLastValue, lastCommitIdLong));
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

        public void UpdateSlot(string slotName, Int64 checkpointToken)
        {
            if (!_slotRebuildTracker[slotName])
            {
                _checkpoints.UpdateMany(
                     Builders<Checkpoint>.Filter.Eq("Slot", slotName),
                     Builders<Checkpoint>.Update.Set("Current", checkpointToken)
                );
            }
            _currentCheckpointTracker.AddOrUpdate(slotName, checkpointToken, (key, value) => checkpointToken);
        }

        public void UpdateSlotAndSetCheckpoint(
            string slotName,
            IEnumerable<String> projectionIdList,
            Int64 valueCheckpointToken,
            Int64? currentCheckpointToken = null)
        {
            _checkpoints.UpdateMany(
                   Builders<Checkpoint>.Filter.Eq("Slot", slotName),
                   Builders<Checkpoint>.Update
                    .Set("Current", currentCheckpointToken ?? valueCheckpointToken)
                    .Set("Value", valueCheckpointToken)
              );
            _currentCheckpointTracker.AddOrUpdate(slotName, currentCheckpointToken ?? 0, (key, value) => currentCheckpointToken ?? 0);
            foreach (var projectionId in projectionIdList)
            {
                _checkpointTracker.AddOrUpdate(projectionId, key => currentCheckpointToken ?? 0, (key, currentValue) => currentCheckpointToken ?? 0);
            }
        }

        public Int64 GetCurrent(IProjection projection)
        {
            var checkpoint = _checkpoints.FindOneById(projection.Info.CommonName);
            if (checkpoint == null)
                return 0;

            return checkpoint.Current ?? 0;
        }

        public void SetCheckpoint(string id, Int64 value)
        {
            _checkpoints.UpdateOne(
                 Builders<Checkpoint>.Filter.Eq("_id", id),
                 Builders<Checkpoint>.Update.Set("Value", value)
            );

            _checkpointTracker.AddOrUpdate(id, key => value, (key, currentValue) => value);
        }

        public CheckPointReplayStatus GetCheckpointStatus(string id, Int64 checkpoint)
        {
            Int64 lastDispatched = _checkpointTracker[id];

            var currentCheckpointValue = checkpoint;
            long lastDispatchedValue = lastDispatched;

            //This is the last checkpoint in rebuild only if the continuous rebuild is not set.
            bool isLast = currentCheckpointValue == lastDispatchedValue && RebuildSettings.ContinuousRebuild == false;
            //is replay is always true if we are in Continuous rebuild.
            bool isReplay = currentCheckpointValue <= lastDispatchedValue || RebuildSettings.ContinuousRebuild;
            _checkpointSlotTracker[_projectionToSlot[id]] = _higherCheckpointToDispatchInRebuild - currentCheckpointValue;
            return new CheckPointReplayStatus(isLast, isReplay);
        }

        public Int64 GetCheckpoint(IProjection projection)
        {
            var id = projection.Info.CommonName;
            if (_checkpointTracker.ContainsKey(id))
                return _checkpointTracker[id];
            return 0;
        }
    }
}

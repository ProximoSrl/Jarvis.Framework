using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.Support;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NEventStore;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public class ConcurrentCheckpointTracker : IConcurrentCheckpointTracker
    {
        readonly MongoCollection<Checkpoint> _checkpoints;
        private ConcurrentDictionary<string, string> _checkpointTracker;
        private ConcurrentDictionary<string, Int64> _checkpointSlotTracker;
        private ConcurrentDictionary<String, String> _projectionToSlot;
        private ConcurrentDictionary<String, Boolean> _slotRebuildTracker;

        public ILogger Logger { get; set; }

        public ConcurrentCheckpointTracker(MongoDatabase db)
        {
            _checkpoints = db.GetCollection<Checkpoint>("checkpoints");
            _checkpoints.CreateIndex(IndexKeys<Checkpoint>.Ascending(x => x.Slot));
            _checkpointTracker = new ConcurrentDictionary<string, string>();
            _checkpointSlotTracker = new ConcurrentDictionary<string, Int64>();
            _projectionToSlot = new ConcurrentDictionary<String, String>();
            _slotRebuildTracker = new ConcurrentDictionary<string, bool>();
            Logger = NullLogger.Instance;
        }

        void Add(IProjection projection, string defaultValue)
        {
            var id = projection.GetCommonName();
            var checkPoint = _checkpoints.FindOneById(id) ?? new Checkpoint(id, defaultValue);

            checkPoint.Signature = projection.GetSignature();
            checkPoint.Slot = projection.GetSlotName();
            _checkpoints.Save(checkPoint);
            _checkpointTracker[id] = checkPoint.Value;
            _projectionToSlot[id] = checkPoint.Slot;
        }

        public void SetUp(IProjection[] projections, int version)
        {
            var versionInfo = _checkpoints.FindOneById("VERSION");
            if (versionInfo == null)
            {
                versionInfo = new Checkpoint("VERSION", "0");
            }
          
            int currentVersion = int.Parse(versionInfo.Value);

            string projectionStartFormCheckpointValue = new LongCheckpoint(0).Value;
            if (currentVersion == 0)
            {
                var eventsVersion = _checkpoints.FindOneById("events");
                if (eventsVersion != null)
                    projectionStartFormCheckpointValue = eventsVersion.Value;
            }

            foreach (var projection in projections)
            {
                Add(projection, projectionStartFormCheckpointValue);
            }

            // mark db
            if (version > currentVersion)
            {
                versionInfo.Value = new LongCheckpoint(version).Value;
                _checkpoints.Save(versionInfo);
            }

            foreach (var slot in projections.Select(p => p.GetSlotName()).Distinct())
            {
                var slotName = slot;
                MetricsHelper.SetCheckpointCountToDispatch(slot, () => GetCheckpointCount(slotName));
                _checkpointSlotTracker[slot] = 0;
                _slotRebuildTracker[slot] = false;
            }

            MetricsHelper.SetCheckpointCountToDispatch("", GetCheckpointMaxCount);
        }

        private double GetCheckpointMaxCount()
        {
            return _checkpointSlotTracker.Select(k => k.Value).Max();
        }

        private double GetCheckpointCount(string slotName)
        {
            return _checkpointSlotTracker[slotName];
        }

        public bool NeedsRebuild(IProjection projection)
        {
            return RebuildSettings.ShouldRebuild;

            //var checkpoint = _checkpoints.FindOneById(projection.GetCommonName());
            //if (checkpoint == null)
            //    return true;

            //var last  = LongCheckpoint.Parse(checkpoint.Value);
            //var current = LongCheckpoint.Parse(checkpoint.Current);

            //return current.LongValue < last.LongValue;
        }

        public void RebuildStarted(IProjection projection, String lastCommitId)
        {
            var projectionName = projection.GetCommonName();
            _slotRebuildTracker[_projectionToSlot[projectionName]] = true;
            _checkpoints.Update(
                Query.EQ("_id", projectionName),
                Update<Checkpoint>.Set(x => x.RebuildStart, DateTime.UtcNow)
                                    .Set(x => x.RebuildStop, null)
                                    .Set(x => x.RebuildTotalSeconds, 0)
                                    .Set(x => x.RebuildActualSeconds, 0)
                                    .Set(x => x.Value, lastCommitId)
                                    .Set(x => x.Current, null)
                                    .Set(x => x.Events, 0)
                                    .Set(x => x.Details, null)
            );
            //Check if some commit was deleted and lastCommitId in Eventstore 
            //is lower than the latest dispatched commit.
            var trackerLastValue = LongCheckpoint.Parse(_checkpointTracker[projection.GetCommonName()]).LongValue;
            var lastCommitIdLong = LongCheckpoint.Parse(lastCommitId).LongValue;
            if (lastCommitIdLong < trackerLastValue) 
            {
                _checkpointTracker[projection.GetCommonName()] = lastCommitId;
            }
        }

  

        public void RebuildEnded(IProjection projection, ProjectionMetrics.Meter meter)
        {
            var projectionName = projection.GetCommonName();
            _slotRebuildTracker[_projectionToSlot[projectionName]] = false;
            var checkpoint = _checkpoints.FindOneById(projectionName);
            checkpoint.RebuildStop = DateTime.UtcNow;
            checkpoint.RebuildTotalSeconds = (long)(checkpoint.RebuildStop - checkpoint.RebuildStart).Value.TotalSeconds;
            checkpoint.Events = meter.TotalEvents;
            checkpoint.RebuildActualSeconds = (double)meter.TotalElapsed / TimeSpan.TicksPerSecond;
            checkpoint.Details = meter;
            _checkpoints.Save(checkpoint);
        }

        public void UpdateSlot(string slotName, string checkpointToken)
        {
            if (_slotRebuildTracker[slotName] == false)
            {
                _checkpoints.Update(
                    Query.EQ("Slot", slotName),
                    Update.Set("Current", checkpointToken),
                    UpdateFlags.Multi
                );
            }
        }

        public string GetCurrent(IProjection projection)
        {
            var checkpoint = _checkpoints.FindOneById(projection.GetCommonName());
            if (checkpoint == null)
                return null;

            return checkpoint.Current;
        }

        public void SetCheckpoint(string id, string value)
        {
            _checkpoints.Update(
                Query.EQ("_id", id),
                Update.Set("Value", value)
            );

            _checkpointTracker.AddOrUpdate(id, key => value, (key, currentValue) => value);
        }

        public void LoadAll()
        {
            var allChecks = _checkpoints.FindAll().OrderBy(x => x.Id);
            _checkpointTracker = new ConcurrentDictionary<string, string>(allChecks.ToDictionary(k => k.Id, v => v.Value));
        }

        public CheckPointReplayStatus GetCheckpointStatus(string id, string checkpoint)
        {
            string lastDispatched = _checkpointTracker[id];

            var currentCheckpointValue = LongCheckpoint.Parse(checkpoint).LongValue;
            long lastDispatchedValue = LongCheckpoint.Parse(lastDispatched).LongValue;

            bool isLast = currentCheckpointValue == lastDispatchedValue;
            bool isReplay = currentCheckpointValue <= lastDispatchedValue;
            _checkpointSlotTracker[_projectionToSlot[id]] = lastDispatchedValue - currentCheckpointValue;
            return new CheckPointReplayStatus(isLast, isReplay);
        }

        public string GetCheckpoint(IProjection projection)
        {
            var id = projection.GetCommonName();
            if (_checkpointTracker.ContainsKey(id))
                return _checkpointTracker[id];
            return null;
        }

        public void Clear()
        {
            _checkpointTracker.Clear();
        }
    }
}

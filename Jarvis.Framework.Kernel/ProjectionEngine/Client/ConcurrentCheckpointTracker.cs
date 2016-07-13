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
using NEventStore;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public class ConcurrentCheckpointTracker : IConcurrentCheckpointTracker
    {
        readonly IMongoCollection<Checkpoint> _checkpoints;
        private ConcurrentDictionary<string, string> _checkpointTracker;
        private ConcurrentDictionary<string, string> _currentCheckpointTracker;

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
            _checkpointTracker = new ConcurrentDictionary<string, string>();
            _checkpointSlotTracker = new ConcurrentDictionary<string, Int64>();
            _projectionToSlot = new ConcurrentDictionary<String, String>();
            _slotRebuildTracker = new ConcurrentDictionary<string, bool>();
            _currentCheckpointTracker = new ConcurrentDictionary<string, string>();
            _checkpointErrors = new List<string>();
            _higherCheckpointToDispatchInRebuild = 0;
        }

        void Add(IProjection projection, string defaultValue)
        {
            var id = projection.GetCommonName();
            var projectionSignature = projection.GetSignature();
            var projectionSlotName = projection.GetSlotName();

            var checkPoint = _checkpoints.FindOneById(id) ?? 
                new Checkpoint(id, defaultValue, projectionSignature);

            //Check if some projection is changed and rebuild is not active
            if (
                checkPoint.Signature != projectionSignature && 
                !RebuildSettings.ShouldRebuild)
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
            var versionInfo = _checkpoints.FindOneById("VERSION");
            if (versionInfo == null)
            {
                versionInfo = new Checkpoint("VERSION", "0", null);
            }

            int currentVersion = int.Parse(versionInfo.Value);

            string projectionStartFormCheckpointValue = new LongCheckpoint(0).Value;
            if (currentVersion == 0)
            {
                var eventsVersion = _checkpoints.FindOneById("events");
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
                versionInfo.Value = new LongCheckpoint(version).Value;
                _checkpoints.Save(versionInfo, versionInfo.Id);
            }

            foreach (var slot in projections.Select(p => p.GetSlotName()).Distinct())
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
            return _currentCheckpointTracker.Values.Select(Int64.Parse).Min();
        }

     
        public Int64 GetMaxDispatchedCheckpoint()
        {
            return _checkpoints.FindAll()
                .ToList()
                .Where(c => !String.IsNullOrEmpty(c.Value) && c.Id != "VERSION")
                .Max(c => LongCheckpoint.Parse(c.Value).LongValue);
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
            var trackerLastValue = LongCheckpoint.Parse(_checkpointTracker[projection.GetCommonName()]).LongValue;
            var lastCommitIdLong = LongCheckpoint.Parse(lastCommitId).LongValue;
            if (trackerLastValue > 0)
            {
                //new projection, it has no dispatched checkpoint.
                _slotRebuildTracker[_projectionToSlot[projectionName]] = true;
            }
            if (lastCommitIdLong < trackerLastValue)
            {
                _checkpointTracker[projection.GetCommonName()] = lastCommitId;
                _checkpoints.UpdateOne(
                   Builders<Checkpoint>.Filter.Eq("_id", projectionName),
                   Builders<Checkpoint>.Update.Set(x => x.Value, lastCommitId));
            }
            _higherCheckpointToDispatchInRebuild = Math.Max(
                _higherCheckpointToDispatchInRebuild,
                Math.Min(trackerLastValue, lastCommitIdLong));
        }



        public void RebuildEnded(IProjection projection, ProjectionMetrics.Meter meter)
        {
            var projectionName = projection.GetCommonName();
            _slotRebuildTracker[_projectionToSlot[projectionName]] = false;
            var checkpoint = _checkpoints.FindOneById(projectionName);
            checkpoint.RebuildStop = DateTime.UtcNow;
            if (checkpoint != null && checkpoint.RebuildStop.HasValue && checkpoint.RebuildStart.HasValue)
            {
                checkpoint.RebuildTotalSeconds = (long)(checkpoint.RebuildStop - checkpoint.RebuildStart).Value.TotalSeconds;
            }

            checkpoint.Events = meter.TotalEvents;
            checkpoint.RebuildActualSeconds = (double)meter.TotalElapsed / TimeSpan.TicksPerSecond;
            checkpoint.Details = meter;
            _checkpoints.Save(checkpoint, checkpoint.Id);
        }

        public void UpdateSlot(string slotName, string checkpointToken)
        {
            if (_slotRebuildTracker[slotName] == false)
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
            string valueCheckpointToken,
            string currentCheckpointToken = null)
        {
            _checkpoints.UpdateMany(
                   Builders<Checkpoint>.Filter.Eq("Slot", slotName),
                   Builders<Checkpoint>.Update
                    .Set("Current", currentCheckpointToken ?? valueCheckpointToken)
                    .Set("Value", valueCheckpointToken )
              );
            _currentCheckpointTracker.AddOrUpdate(slotName, currentCheckpointToken, (key, value) => currentCheckpointToken);
            foreach (var projectionId in projectionIdList)
            {
                _checkpointTracker.AddOrUpdate(projectionId, key => currentCheckpointToken, (key, currentValue) => currentCheckpointToken);
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
            _checkpoints.UpdateOne(
                 Builders<Checkpoint>.Filter.Eq("_id", id),
                 Builders<Checkpoint>.Update.Set("Value", value)
            );

            _checkpointTracker.AddOrUpdate(id, key => value, (key, currentValue) => value);
        }

        public CheckPointReplayStatus GetCheckpointStatus(string id, string checkpoint)
        {
            string lastDispatched = _checkpointTracker[id];

            var currentCheckpointValue = LongCheckpoint.Parse(checkpoint).LongValue;
            long lastDispatchedValue = LongCheckpoint.Parse(lastDispatched).LongValue;

            bool isLast = currentCheckpointValue == lastDispatchedValue;
            bool isReplay = currentCheckpointValue <= lastDispatchedValue;
            _checkpointSlotTracker[_projectionToSlot[id]] = _higherCheckpointToDispatchInRebuild - currentCheckpointValue;
            return new CheckPointReplayStatus(isLast, isReplay);
        }

        public string GetCheckpoint(IProjection projection)
        {
            var id = projection.GetCommonName();
            if (_checkpointTracker.ContainsKey(id))
                return _checkpointTracker[id];
            return null;
        }

        public List<string> GetCheckpointErrors()
        {
            if (RebuildSettings.ShouldRebuild == false)
            {
                //need to check every slot for new projection 
                var slots = _projectionToSlot.GroupBy(k => k.Value, k => k.Key);
                foreach (var slot in slots)
                {
                    var allCheckpoints = _checkpointTracker
                        .Where(k => slot.Contains(k.Key));

                    var minCheckpoint = allCheckpoints.Min(k => k.Value);
                    var maxCheckpoint = allCheckpoints.Max(k => k.Value);

                    if (minCheckpoint != maxCheckpoint)
                    {
                        String error;
                        //error, ve have not all projection at the same checkpoint 
                        if (allCheckpoints
                            .Where(k => k.Value != "0")
                            .Select(k => k.Value)
                            .Distinct().Count() == 1)
                        {
                            //We have one or more new projection in slot
                            error = String.Format("Error in slot {0}: we have new projections at checkpoint 0.\n REBUILD NEEDED!", slot.Key);
                        }
                        else
                        {
                            error = String.Format("Error in slot {0}, not all projection at the same checkpoint value. Please check reamodel db!", slot.Key);
                        }
                        _checkpointErrors.Add(error);
                    }
                }
            }
            return _checkpointErrors;
        }

        public CheckpointSlotStatus GetSlotsStatus(IProjection[] projections)
        {
            CheckpointSlotStatus returnValue = new CheckpointSlotStatus();
            //we need to group projection per slot
            var allCheckpoint = _checkpoints.FindAll().ToEnumerable().ToDictionary(c => c.Id, c => c);
            var slots = projections.GroupBy(
                p => p.GetSlotName(),
                p => new {
                    Projection = p,
                    Checkpoint = allCheckpoint.ContainsKey(p.GetCommonName()) ? allCheckpoint[p.GetCommonName()] : null, 
                });
            Int64 maxCheckpoint = 0;
            if (allCheckpoint.Any())
                maxCheckpoint = allCheckpoint.Select(c => Int64.Parse(c.Value.Value)).Max();
            if (maxCheckpoint > 0) //if we have no dispatched commit, we have no need to rebuild or do anything
            {
                foreach (var slot in slots)
                {
                    if (slot.All(s => s.Checkpoint == null || s.Checkpoint.Value == "0"))
                    {
                        returnValue.NewSlots.Add(slot.Key);
                    }
                    else if (slot.Where(s => s.Checkpoint != null).Select(s => s.Checkpoint.Value).Distinct().Count() > 1)
                    {
                        returnValue.SlotsWithErrors.Add(new CheckpointSlotStatus.SlotError()
                        {
                            SlotName = slot.Key,
                            Errors = String.Format("Error in slot {0}, not all projection at the same checkpoint value. Please check reamodel db!", slot.Key),
                        });
                    }
                    else if (slot.Any(s => s.Checkpoint == null || s.Checkpoint.Signature != s.Projection.GetSignature()))
                    {
                        returnValue.SlotsThatNeedsRebuild.Add(slot.Key);
                    }
                }
            }
            return returnValue;
        }
    }
}

using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Jarvis.Framework.Kernel.Support
{
    /// <inheritdoc />
    public class ProjectionStatusLoader : IProjectionStatusLoader
    {
        private readonly int _pollingIntervalInSeconds;
        private readonly IMongoCollection<BsonDocument> _commitsCollection;
        private readonly IMongoCollection<BsonDocument> _checkpointCollection;

        private readonly Dictionary<String, SlotStatus> _lastMetrics;
        private readonly IMongoDatabase _eventStoreDatabase;

        private DateTime _lastPoll = DateTime.MinValue;
        private Int32 _isRetrievingData = 0;

        /// <summary>
        /// This is an optional dependency, if we are into the process that runs projection
        /// engine we can read data from memory
        /// </summary>
        public IConcurrentCheckpointTracker ConcurrentCheckpointTracker { get; set; }

        /// <summary>
        /// This will be different from null if we are in projection engine process and
        /// we have Concurrent Checkpoint tracker configured.
        /// </summary>
        public IProjection[] AllProjections { get; set; }

        public ProjectionStatusLoader(
            IMongoDatabase eventStoreDatabase,
            IMongoDatabase readModelDatabase,
            Int32 pollingIntervalInSeconds = 10)
        {
            _eventStoreDatabase = eventStoreDatabase;
            _pollingIntervalInSeconds = pollingIntervalInSeconds;
            _commitsCollection = eventStoreDatabase.GetCollection<BsonDocument>(EventStoreFactory.PartitionCollectionName);
            _checkpointCollection = readModelDatabase.GetCollection<BsonDocument>("checkpoints");
            _lastMetrics = new Dictionary<string, SlotStatus>();
        }

        public IEnumerable<SlotStatus> GetSlotMetrics()
        {
            GetMetricValue();
            return _lastMetrics.Values.ToList();
        }

        public SlotStatus GetSlotMetric(string slotName)
        {
            GetMetricValue();
            if (_lastMetrics.TryGetValue(slotName, out var value))
            {
                return value;
            }
            return SlotStatus.Null;
        }

        private Dictionary<string, IProjection> _sampleProjectionPerSlot;

        private void InitProjectionDictionary()
        {
            if (_sampleProjectionPerSlot == null)
            {
                if (AllProjections == null)
                {
                    _sampleProjectionPerSlot = new Dictionary<string, IProjection>();
                }
                else
                {
                    //We need to have a single projection for each slot to query the status to the
                    //Concurrent checkpoint tracker
                    _sampleProjectionPerSlot = AllProjections
                        .Select(p => new
                        {
                            Projection = p,
                            Slot = p.GetType().GetCustomAttribute<ProjectionInfoAttribute>().SlotName
                        })
                        .GroupBy(e => e.Slot)
                        .ToDictionary(e => e.Key, e => e.First().Projection);
                }
            }
        }

        private void GetMetricValue()
        {
            if (Interlocked.CompareExchange(ref _isRetrievingData, 1, 0) == 0)
            {
                try
                {
                    if (DateTime.UtcNow.Subtract(_lastPoll).TotalSeconds > _pollingIntervalInSeconds)
                    {
                        //common code, grab latest commit
                        if (_eventStoreDatabase.Client.Cluster.Description.State != ClusterState.Connected)
                        {
                            //database is down, we cannot read values.
                            return;
                        }

                        var lastCommitDoc = _commitsCollection
                            .FindAll()
                            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                            .Project(Builders<BsonDocument>.Projection.Include("_id"))
                            .FirstOrDefault();
                        if (lastCommitDoc == null) return;

                        var lastCommit = lastCommitDoc["_id"].AsInt64;

                        var someProjectionRegistered = AllProjections?.Length > 0;
                        if (ConcurrentCheckpointTracker != null && someProjectionRegistered)
                        {
                            UpdateVaueQueryingConcurrentCheckpointTracker(lastCommit);
                        }
                        else
                        {
                            UpdateValueQueryingTheDatabase(lastCommit);
                        }
                    }
                }
                finally
                {
                    _lastPoll = DateTime.UtcNow;
                    Interlocked.Exchange(ref _isRetrievingData, 0);
                }
            }
        }

        private void UpdateVaueQueryingConcurrentCheckpointTracker(long lastCommit)
        {
            //read data directly from the concurrent checkpoint tracker.
            //first be sure to have preloaded a dictionary with slot in key and 
            //one of the projection as value.
            InitProjectionDictionary();
            var allSlot = _sampleProjectionPerSlot
                .Select(kvp => new
                {
                    SlotName = kvp.Key,
                    SlotCheckpoint = ConcurrentCheckpointTracker.GetCheckpoint(kvp.Value)
                });

            foreach (var slotStatus in allSlot)
            {
                UpdateSlot(lastCommit, slotStatus.SlotName, slotStatus.SlotCheckpoint);
            }
        }

        private void UpdateValueQueryingTheDatabase(long lastCommit)
        {
            BsonDocument[] pipeline =
            {
                BsonDocument.Parse(@"{""Active"": true}"),
                BsonDocument.Parse(@"{""Slot"" : 1, ""Current"" : 1}"),
                BsonDocument.Parse(@"{""_id"" : ""$Slot"", ""Current"" : {$min : ""$Current""}}")
            };

            var options = new AggregateOptions();
            options.AllowDiskUse = true;
            options.UseCursor = true;
            var allCheckpoints = _checkpointCollection.Aggregate(options)
                .Match(pipeline[0])
                .Project(pipeline[1])
                .Group(pipeline[2])
                .ToList();
            foreach (BsonDocument metric in allCheckpoints)
            {
                var slotName = metric["_id"].AsString;
                Int64 current;
                if (!metric["Current"].IsBsonNull)
                {
                    current = metric["Current"].AsInt64;
                }
                else
                {
                    current = 0;
                }

                UpdateSlot(lastCommit, slotName, current);
            }
        }

        private void UpdateSlot(long lastCommit, string slotName, long current)
        {
            var delay = lastCommit - current;
            _lastMetrics[slotName] = new SlotStatus(slotName, delay);
        }
    }
}
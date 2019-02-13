using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Driver.Core.Clusters;
using System.Threading;
using Jarvis.Framework.Kernel.Engine;

namespace Jarvis.Framework.Kernel.Support
{
    public class ProjectionStatusLoader : IProjectionStatusLoader
    {
        private readonly int _pollingIntervalInSeconds;
        private readonly IMongoCollection<BsonDocument> _commitsCollection;
        private readonly IMongoCollection<BsonDocument> _checkpointCollection;

        private readonly Dictionary<String, SlotStatus> _lastMetrics;
        private readonly IMongoDatabase _eventStoreDatabase;
        private readonly IMongoDatabase _readModelDatabase;

        private DateTime lastPoll = DateTime.MinValue;
        private Int32 _isRetrievingData = 0;
        private Int64 _lastDelay;

        public ProjectionStatusLoader(
            IMongoDatabase eventStoreDatabase,
            IMongoDatabase readModelDatabase,
            Int32 pollingIntervalInSeconds = 5)
        {
            _eventStoreDatabase = eventStoreDatabase;
            _readModelDatabase = readModelDatabase;
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
            if (!_lastMetrics.ContainsKey(slotName)) return SlotStatus.Null;

            return _lastMetrics[slotName];
        }

        public long GetMaxCheckpointToDispatch()
        {
            GetMetricValue();
            return _lastDelay;
        }

        private void GetMetricValue()
        {
            try
            {
                if (Interlocked.CompareExchange(ref _isRetrievingData, 1, 0) == 0 
                    && DateTime.Now.Subtract(lastPoll).TotalSeconds > _pollingIntervalInSeconds)
                {
                    if (_eventStoreDatabase.Client.Cluster.Description.State != ClusterState.Connected ||
                        _readModelDatabase.Client.Cluster.Description.State != ClusterState.Connected)
                    {
                        //database is down, we cannot read values.
                        lastPoll = DateTime.Now;
                        return;
                    }

                    _lastDelay = 0;

                    var lastCommitDoc = _commitsCollection
                        .FindAll()
                        .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                        .Project(Builders<BsonDocument>.Projection.Include("_id"))
                        .FirstOrDefault();
                    if (lastCommitDoc == null) return;

                    var lastCommit = lastCommitDoc["_id"].AsInt64;

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

                        var delay = lastCommit - current;
                        if (delay > _lastDelay) _lastDelay = delay;
                        _lastMetrics[slotName] = new SlotStatus(slotName, delay);
                    }
                    lastPoll = DateTime.Now;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isRetrievingData, 0);
            }
        }
    }
}
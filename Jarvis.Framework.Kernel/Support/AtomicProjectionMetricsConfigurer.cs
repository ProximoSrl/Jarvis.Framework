using Castle.Core;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Metrics;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Jarvis.Framework.Kernel.Support
{
    public class ProjectionTargetCheckpointLoader : IProjectionTargetCheckpointLoader
    {
        private readonly IMongoCollection<BsonDocument> _commitsCollection;

        public ProjectionTargetCheckpointLoader(IMongoDatabase eventStoreDatabase)
        {
            _commitsCollection = eventStoreDatabase.GetCollection<BsonDocument>(EventStoreFactory.PartitionCollectionName);
        }

        public long GetMaxCheckpointToDispatch()
        {
            var lastCommitDoc = _commitsCollection
                .FindAll()
                .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                .Project(Builders<BsonDocument>.Projection.Include("_id"))
                .FirstOrDefault();
            if (lastCommitDoc == null)
            {
                return 0;
            }

            return lastCommitDoc["_id"].AsInt64;
        }
    }

    public class AtomicReadModelVersionLoader
    {
        private readonly IMongoDatabase _readmodelDb;
        private readonly ConcurrentDictionary<String, IMongoCollection<AbstractAtomicReadModel>> _collectionsCache;

        public AtomicReadModelVersionLoader(IMongoDatabase readmodelDb)
        {
            _readmodelDb = readmodelDb;
            _collectionsCache = new ConcurrentDictionary<string, IMongoCollection<AbstractAtomicReadModel>>();
        }

        public double CountReadModelToUpdateByName(string readmodelName, Int32 version)
        {
            var _collection = GetCollection(readmodelName);
            return _collection.AsQueryable()
                 .Where(_ => _.ReadModelVersion != version)
                 .Count();
        }

        private IMongoCollection<AbstractAtomicReadModel> GetCollection(string readmodelName)
        {
            if (!_collectionsCache.TryGetValue(readmodelName, out var collection))
            {
                collection = _readmodelDb.GetCollection<AbstractAtomicReadModel>(readmodelName);
                _collectionsCache.TryAdd(readmodelName, collection);
            }
            return collection;
        }
    }

    public class AtomicProjectionMetricsConfigurer : IStartable
    {
        private readonly IProjectionTargetCheckpointLoader _checkPointLoader;
        private readonly AtomicProjectionCheckpointManager _atomicProjectionCheckpointManager;
        private readonly AtomicReadModelVersionLoader _versionLoader;
        private readonly IAtomicReadModelFactory _readModelFactory;

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public AtomicProjectionMetricsConfigurer(
                IProjectionTargetCheckpointLoader checkPointLoader,
                IAtomicReadModelFactory readModelFactory,
                AtomicProjectionCheckpointManager atomicProjectionCheckpointManager,
                AtomicReadModelVersionLoader versionLoader)
        {
            _checkPointLoader = checkPointLoader;
            _atomicProjectionCheckpointManager = atomicProjectionCheckpointManager;
            _versionLoader = versionLoader;
            _readModelFactory = readModelFactory;
        }

        public void Start()
        {
            Metric.Gauge("checkpoint-behind-AtomicReadModels", CheckAllAtomicsValue(), Unit.Items);
            HealthChecks.RegisterHealthCheck("Slot-All-AtomicReadmodels", CheckSlotHealth());
            foreach (var readModelType in _atomicProjectionCheckpointManager.GetAllRegisteredAtomicReadModels())
            {
                var name = CollectionNames.GetCollectionName(readModelType);
                //try to create an instance to grab the version
                try
                {
                    var readmodelVersion = _readModelFactory.GetReamdodelVersion(readModelType);
                    Metric.Gauge("versions-behind-" + name, () => _versionLoader.CountReadModelToUpdateByName(name, readmodelVersion), Unit.Items);
                }
                catch (Exception ex)
                {
                    Logger.ErrorFormat(ex, "Unable to setup metric for atomic readmodel {0}", readModelType.FullName);
                }
            }
        }

        public void Stop()
        {
            // Method intentionally left empty.
        }

        private Func<HealthCheckResult> CheckSlotHealth()
        {
            return () =>
            {
                long maxCheckpoint = _checkPointLoader.GetMaxCheckpointToDispatch();
                long minimumDispateched = _atomicProjectionCheckpointManager.GetMinimumPositionDispatched();
                long behind = maxCheckpoint - minimumDispateched;
                if (minimumDispateched > maxCheckpoint)
                {
                    return HealthCheckResult.Unhealthy("Slot-All-AtomicReadmodels behind:" + behind);
                }
                else
                {
                    return HealthCheckResult.Healthy("Slot-All-AtomicReadmodels behind:" + behind);
                }
            };
        }

        private Func<double> CheckAllAtomicsValue()
        {
            return () =>
            {
                long maxCheckpoint = _checkPointLoader.GetMaxCheckpointToDispatch();
                long minimumDispateched = _atomicProjectionCheckpointManager.GetMinimumPositionDispatched();
                return maxCheckpoint - minimumDispateched;
            };
        }
    }
}

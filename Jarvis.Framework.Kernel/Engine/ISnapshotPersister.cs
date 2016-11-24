using Jarvis.NEventStoreEx.CommonDomainEx;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using NEventStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Kernel.ProjectionEngine.Unfolder;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using Castle.Core.Logging;

namespace Jarvis.Framework.Kernel.Engine
{

    public class MongoSnapshotPersisterProvider : ISnapshotPersister
    {
        #region Aux classes

        private class SnapshotPersister
        {
            public ObjectId Id { get; set; }

            public String StreamId { get; set; }

            public String Type { get; set; }

            public string BucketId { get; set; }

            public int StreamRevision { get; set; }

            public object Payload { get;  set; }
        }

        #endregion

        private readonly IMongoDatabase _mongoDatabase;

        private readonly IMongoCollection<SnapshotPersister> _snapshotCollection;

        private readonly ILogger _logger;

        public MongoSnapshotPersisterProvider(IMongoDatabase mongoDatabase, ILogger logger)
        {
            _mongoDatabase = mongoDatabase;
            _snapshotCollection = _mongoDatabase.GetCollection<SnapshotPersister>("Snapshots");
            _snapshotCollection.Indexes.CreateOne(Builders<SnapshotPersister>.IndexKeys
                .Ascending(s => s.StreamId)
                .Ascending(s => s.Type)
                .Ascending(s => s.StreamRevision));
            this._logger = logger;
        }

        public void Clear(string streamId, Int32 versionUpTo, String type)
        {
            _snapshotCollection.DeleteMany(Builders<SnapshotPersister>.Filter
                .And(
                    Builders<SnapshotPersister>.Filter.Eq(s => s.StreamId, streamId),
                    Builders<SnapshotPersister>.Filter.Eq(s => s.Type, type),
                    Builders<SnapshotPersister>.Filter.Lte(s => s.StreamRevision, versionUpTo)
                ));
        }

        public ISnapshot Load(string streamId, Int32 versionUpTo, String type)
        {
            try
            {
                var record = _snapshotCollection.Find(Builders<SnapshotPersister>.Filter
                .And(
                    Builders<SnapshotPersister>.Filter.Eq(s => s.StreamId, streamId),
                    Builders<SnapshotPersister>.Filter.Eq(s => s.Type, type),
                    Builders<SnapshotPersister>.Filter.Lte(s => s.StreamRevision, versionUpTo)
                ))
                .Sort(Builders<SnapshotPersister>.Sort.Descending(s => s.StreamRevision))
                .FirstOrDefault();
                if (record == null) return null;

                return new Snapshot(record.BucketId, record.StreamId, record.StreamRevision, record.Payload);
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Unable to load snapshot for stream {0}", streamId);
            }
            return null;
        }

        public void Persist(ISnapshot snapshot, String type)
        {
            try
            {
                SnapshotPersister record = new SnapshotPersister();
                record.Id = ObjectId.GenerateNewId();
                record.Type = type;
                record.StreamId = snapshot.StreamId;
                record.StreamRevision = snapshot.StreamRevision;
                record.Payload = snapshot.Payload;
                record.BucketId = snapshot.BucketId;
                _snapshotCollection.InsertOne(record);
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Unable to persist snapshot for stream {0}", snapshot.StreamId);
            }
           
        }
    }

  
}

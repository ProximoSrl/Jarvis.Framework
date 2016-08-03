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

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// This interface abstract the concept of a class that it is able to 
    /// persist and load <see cref="ISnapshot"/> instances.
    /// </summary>
    public interface ISnapshotPersister
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="type">We can have different type of snapshot, for standard aggregate or for 
        /// event unfolder and this parameter allows the caller to specify a type that generates the snapshot</param>
        void Persist(ISnapshot snapshot, String type);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="version">If you need to restore an aggregate at version X you 
        /// should get a snapshot that is not greater than X. This parameter allow you to avoid 
        /// loading a snapshot taken at version token greater than this value.</param>
        /// <returns></returns>
        ISnapshot Load(String streamId, Int32 version, String type);

        /// <summary>
        /// Clear all checkpoints taken before a certain checkpoint. 
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="version"></param>
        void Clear(String streamId, Int32 version, String type);
    }

    public class NullSnapshotPersister : ISnapshotPersister
    {
        public static readonly NullSnapshotPersister Instance;

        static NullSnapshotPersister()
        {
            Instance = new NullSnapshotPersister();
        }

        public void Clear(string streamId, Int32 version, String type)
        {
           
        }

        public ISnapshot Load(string streamId, Int32 version, String type)
        {
            return null;
        }

        public void Persist(ISnapshot snapshot, String type)
        {
            
        }
    }

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

        public MongoSnapshotPersisterProvider(IMongoDatabase mongoDatabase)
        {
            _mongoDatabase = mongoDatabase;
            _snapshotCollection = _mongoDatabase.GetCollection<SnapshotPersister>("Snapshots");
            _snapshotCollection.Indexes.CreateOne(Builders<SnapshotPersister>.IndexKeys
                .Ascending(s => s.StreamId)
                .Ascending(s => s.Type)
                .Ascending(s => s.StreamRevision));
        }

        public void Clear(string streamId, Int32 version, String type)
        {
            _snapshotCollection.DeleteMany(Builders<SnapshotPersister>.Filter
                .And(
                    Builders<SnapshotPersister>.Filter.Eq(s => s.StreamId, streamId),
                    Builders<SnapshotPersister>.Filter.Eq(s => s.Type, type),
                    Builders<SnapshotPersister>.Filter.Lte(s => s.StreamRevision, version)
                ));
        }

        public ISnapshot Load(string streamId, Int32 version, String type)
        {
            var record =  _snapshotCollection.Find(Builders<SnapshotPersister>.Filter
                .And(
                    Builders<SnapshotPersister>.Filter.Eq(s => s.StreamId, streamId),
                    Builders<SnapshotPersister>.Filter.Eq(s => s.Type, type),
                    Builders<SnapshotPersister>.Filter.Lte(s => s.StreamRevision, version)
                ))
                .Sort(Builders<SnapshotPersister>.Sort.Descending(s => s.StreamRevision))
                .FirstOrDefault();
            if (record == null) return null;

            return new Snapshot(record.BucketId, record.StreamId, record.StreamRevision, record.Payload);
        }

        public void Persist(ISnapshot snapshot, String type)
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
    }

  
}

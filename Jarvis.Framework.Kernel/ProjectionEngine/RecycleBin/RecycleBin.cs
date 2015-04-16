using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;

namespace Jarvis.Framework.Kernel.ProjectionEngine.RecycleBin
{
    public class SlotId
    {
        public string StreamId { get; private set; }
        public string BucketId { get; private set; }

        public SlotId(string streamId, string bucketId)
        {
            StreamId = streamId;
            BucketId = bucketId;
        }
    }

    public class RecycleBinSlot
    {
        public SlotId Id { get; private set; }
        public DateTime DeletedAt { get; private set; }
        public IDictionary<string, object> Data { get; private set; }

        public RecycleBinSlot(SlotId id, DateTime deleteDateTime, IDictionary<string, object> data)
        {
            Id = id;
            this.Data = data;
            this.DeletedAt = deleteDateTime;
        }
    }

    public interface IRecycleBin
    {
        IQueryable<RecycleBinSlot> Slots { get; }
        void Delete(string aggregateId, string bucketId, DateTime deleteDateTime, object data = null);
        void Drop();
        void Purge(SlotId id);
    }

    public class RecycleBin : IRecycleBin
    {
        readonly MongoCollection<RecycleBinSlot> _collection;

        public RecycleBin(MongoDatabase db)
        {
            _collection = db.GetCollection<RecycleBinSlot>("RecycleBin");
        }

        public IQueryable<RecycleBinSlot> Slots {
            get { return _collection.AsQueryable(); }
        }

        public void Delete(string aggregateId, string bucketId, DateTime deleteDateTime, object data = null)
        {
            IDictionary<string, object> dictionary = null;

            if (data != null)
            {
                dictionary = data.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(data));
            }

            _collection.Save(new RecycleBinSlot(new SlotId(aggregateId, bucketId), deleteDateTime, dictionary));
        }

        public void Drop()
        {
            _collection.Drop();
        }

        public void Purge(SlotId id)
        {
            _collection.Remove(Query.EQ("_id", id.ToBsonDocument()));
        }

        public void SetUp()
        {
        
        }
    }
}

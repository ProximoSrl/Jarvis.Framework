using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Jarvis.Framework.Shared.Helpers;

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
        readonly IMongoCollection<RecycleBinSlot> _collection;

        public RecycleBin(IMongoDatabase db)
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

            var id = new SlotId(aggregateId, bucketId);
            _collection.Save(new RecycleBinSlot(id, deleteDateTime, dictionary), id);
        }

        public void Drop()
        {
            _collection.Drop();
        }

        public void Purge(SlotId id)
        {
            _collection.RemoveById(id);
        }

        public void SetUp()
        {
        
        }
    }
}

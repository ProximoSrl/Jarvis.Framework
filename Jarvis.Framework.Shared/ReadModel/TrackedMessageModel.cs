using System;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IMessagesTracker
    {
        void Started(IMessage msg);
        void Completed(Guid commandId, DateTime completedAt);
        bool Dispatched(Guid commandId, DateTime dispatchedAt);
        void Drop();
        void Failed(Guid commandId, DateTime failedAt, Exception ex);
    }

    public class TrackedMessageModel
    {
        public ObjectId Id { get; set; }
        public string MessageId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public DateTime? FailedAt { get; set; }

        public IMessage Message { get; set; }
        public string Description { get; set; }
        public string IssuedBy { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class MongoDbMessagesTracker : IMessagesTracker
    {
        readonly IMongoCollection<TrackedMessageModel> _commands;

        public MongoDbMessagesTracker(IMongoDatabase db)
        {
            _commands = db.GetCollection<TrackedMessageModel>("messages");
            _commands.Indexes.CreateOne(Builders<TrackedMessageModel>.IndexKeys.Ascending(x => x.MessageId));
            _commands.Indexes.CreateOne(Builders<TrackedMessageModel>.IndexKeys.Ascending(x => x.IssuedBy));
        }

        public void Started(IMessage msg)
        {
            var id = msg.MessageId.ToString();
            string issuedBy = null;

            if (msg is ICommand)
            {
                issuedBy = ((ICommand) msg).GetContextData("user.id");
            }
            else if (msg is IDomainEvent)
            {
                issuedBy = ((IDomainEvent) msg).IssuedBy;
            }

            _commands.UpdateOne(
               Builders<TrackedMessageModel>.Filter.Eq(x=>x.MessageId, id),
               Builders<TrackedMessageModel >.Update
                    .Set(x => x.Message, msg)
                    .Set(x=>x.StartedAt, DateTime.UtcNow)
                    .Set(x => x.IssuedBy, issuedBy)
                    .Set(x => x.Description, msg.Describe()),
               new UpdateOptions() { IsUpsert = true} 
               );
        }

        public void Completed(Guid commandId, DateTime completedAt)
        {
            var id = commandId.ToString();
            _commands.UpdateOne(
                Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                Builders<TrackedMessageModel>.Update.Set(x => x.CompletedAt, completedAt),
                new UpdateOptions() { IsUpsert = true }
            );
        }

        public bool Dispatched(Guid commandId, DateTime dispatchedAt)
        {
            var id = commandId.ToString();
            var result = _commands.UpdateOne(
                 Builders<TrackedMessageModel>.Filter.And(
                    Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                     Builders<TrackedMessageModel>.Filter.Eq(x => x.DispatchedAt, null)
                ),
                Builders<TrackedMessageModel>.Update.Set(x => x.DispatchedAt, dispatchedAt)
            );

            return result.ModifiedCount > 0;
        }

        public void Drop()
        {
            _commands.Drop();
        }

        public void Failed(Guid commandId, DateTime failedAt, Exception ex)
        {
            var id = commandId.ToString();
            _commands.UpdateOne(
                Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                Builders<TrackedMessageModel>.Update
                    .Set(x => x.FailedAt, failedAt)
                    .Set(x => x.ErrorMessage, ex.Message),
                new UpdateOptions() { IsUpsert = true}
            );
        }
    }

    public class NullMessageTracker : IMessagesTracker 
    {
        public static NullMessageTracker Instance { get; set; }

        static NullMessageTracker() 
        {
            Instance = new NullMessageTracker();
        }

        public void Started(IMessage msg)
        {
           
        }

        public void Completed(Guid commandId, DateTime completedAt)
        {
            
        }

        public bool Dispatched(Guid commandId, DateTime dispatchedAt)
        {
            return true;
        }

        public void Drop()
        {
            
        }

        public void Failed(Guid commandId, DateTime failedAt, Exception ex)
        {
            
        }
    }
}

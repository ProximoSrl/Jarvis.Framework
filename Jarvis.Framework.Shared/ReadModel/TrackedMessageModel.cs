using System;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

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
        readonly MongoCollection<TrackedMessageModel> _commands;

        public MongoDbMessagesTracker(MongoDatabase db)
        {
            _commands = db.GetCollection<TrackedMessageModel>("messages");
            _commands.CreateIndex(IndexKeys<TrackedMessageModel>.Ascending(x => x.MessageId));
            _commands.CreateIndex(IndexKeys<TrackedMessageModel>.Ascending(x => x.IssuedBy));
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

            _commands.Update(
                Query<TrackedMessageModel>.EQ(x=>x.MessageId, id),
                Update<TrackedMessageModel>
                    .Set(x => x.Message, msg)
                    .Set(x=>x.StartedAt, DateTime.UtcNow)
                    .Set(x => x.IssuedBy, issuedBy)
                    .Set(x => x.Description, msg.Describe()),
                UpdateFlags.Upsert
            );
        }

        public void Completed(Guid commandId, DateTime completedAt)
        {
            var id = commandId.ToString();
            _commands.Update(
                Query<TrackedMessageModel>.EQ(x => x.MessageId, id),
                Update<TrackedMessageModel>.Set(x => x.CompletedAt, completedAt),
                UpdateFlags.Upsert
            );
        }

        public bool Dispatched(Guid commandId, DateTime dispatchedAt)
        {
            var id = commandId.ToString();
            var result = _commands.Update(
                Query.And(
                    Query<TrackedMessageModel>.EQ(x => x.MessageId, id),
                    Query<TrackedMessageModel>.EQ(x => x.DispatchedAt, null)
                ),
                Update<TrackedMessageModel>.Set(x => x.DispatchedAt, dispatchedAt)
            );

            return result.DocumentsAffected > 0;
        }

        public void Drop()
        {
            _commands.Drop();
        }

        public void Failed(Guid commandId, DateTime failedAt, Exception ex)
        {
            var id = commandId.ToString();
            _commands.Update(
                Query<TrackedMessageModel>.EQ(x => x.MessageId, id),
                Update<TrackedMessageModel>
                    .Set(x => x.FailedAt, failedAt)
                    .Set(x => x.ErrorMessage, ex.Message),
                UpdateFlags.Upsert
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

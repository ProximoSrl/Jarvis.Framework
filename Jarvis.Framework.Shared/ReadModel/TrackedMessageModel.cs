using System;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Metrics;
using MongoDB.Bson.Serialization;
using System.Linq;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IMessagesTracker
    {

        /// <summary>
        /// A command was sent to the bus, this is the first event that is raised.
        /// </summary>
        /// <param name="msg"></param>
        void Started(IMessage msg);

        /// <summary>
        /// This is called from the real Command Handler adapted, it is the timestamp
        /// of the system when the message is going to be elaborated.
        /// 
        /// It can be called multiple times, if command execution has conflicts and needs
        /// to have a retry.
        /// </summary>
        /// <param name="commandId"></param>
        /// <param name="startAt"></param>
        void ElaborationStarted(Guid commandId, DateTime startAt);

        /// <summary>
        /// Message was elaborated with success
        /// </summary>
        /// <param name="commandId"></param>
        /// <param name="completedAt"></param>
        void Completed(Guid commandId, DateTime completedAt);

        /// <summary>
        /// Dispatched is the status when the event related to the command is 
        /// dispatched by the NotifyCommitHandled in projection engine. This means
        /// that the command is executed then dispatched to the bus and if there
        /// is a Reply-to a reply command is sent.
        /// </summary>
        /// <param name="commandId"></param>
        /// <param name="dispatchedAt"></param>
        /// <returns></returns>
        bool Dispatched(Guid commandId, DateTime dispatchedAt);

        /// <summary>
        /// Drop the entire collection.
        /// </summary>
        void Drop();

        /// <summary>
        /// Message cannot be elaborated, some error prevents the message to be
        /// handled.
        /// </summary>
        /// <param name="commandId"></param>
        /// <param name="failedAt"></param>
        /// <param name="ex"></param>
        void Failed(Guid commandId, DateTime failedAt, Exception ex);
    }

    public class TrackedMessageModel
    {
        public ObjectId Id { get; set; }
        public string MessageId { get; set; }

        /// <summary>
        /// Timestamp when message is "started", with bus it is the time the message is sent to the bus
        /// this is the timestamp the message is generated.
        /// </summary>
        /// <param name="msg"></param>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// This is an array because the command can have retry, due to conflicts. This property stores
        /// all the execution start time for the command
        /// </summary>
        public DateTime[] ExecutionStartTimeList { get; set; }

        /// <summary>
        /// Last execution start time. 
        /// </summary>
        public DateTime? LastExecutionStartTime { get; set; }

        /// <summary>
        /// In case of retry, this value is greater than 1 
        /// </summary>
        public Int32 ExecutionCount { get; set; }

        /// <summary>
        /// Time of completion of the command
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Time of final dispatch of the command, this is the last message.
        /// </summary>
        public DateTime? DispatchedAt { get; set; }

        /// <summary>
        /// Timestamp of failure if the command cannot be executed.
        /// </summary>
        public DateTime? FailedAt { get; set; }

        public IMessage Message { get; set; }
        public string Description { get; set; }
        public string IssuedBy { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class MongoDbMessagesTracker : IMessagesTracker
    {
        readonly MongoCollection<TrackedMessageModel> _commands;

        private static readonly Timer queueTimer = Metric.Timer("CommandWaitInQueue", Unit.Commands);
        private static readonly Timer totalExecutionTimer = Metric.Timer("CommandTotalExecution", Unit.Commands);
        private static readonly Counter queueCounter = Metric.Counter("CommandsWaitInQueue", Unit.Custom("ms"));
        private static readonly Counter totalExecutionCounter = Metric.Counter("CommandsTotalExecution", Unit.Custom("ms"));

        private static readonly Meter errorMeter = Metric.Meter("CommandFailures", Unit.Commands);

        //private static readonly Counter retryCount = Metric.Counter("CommandsRetry", Unit.Custom("ms"));

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
                issuedBy = ((ICommand)msg).GetContextData("user.id");
            }
            else if (msg is IDomainEvent)
            {
                issuedBy = ((IDomainEvent)msg).IssuedBy;
            }

            _commands.Update(
                Query<TrackedMessageModel>.EQ(x => x.MessageId, id),
                Update<TrackedMessageModel>
                    .Set(x => x.Message, msg)
                    .Set(x => x.StartedAt, DateTime.UtcNow)
                    .Set(x => x.IssuedBy, issuedBy)
                    .Set(x => x.Description, msg.Describe()),
                UpdateFlags.Upsert
            );
        }

        public void ElaborationStarted(Guid commandId, DateTime startAt)
        {
            var id = commandId.ToString();
            var updated = _commands.Update(
                Query<TrackedMessageModel>.EQ(x => x.MessageId, id),
                Update<TrackedMessageModel>
                    .Set(x => x.LastExecutionStartTime, startAt)
                    .Push(x => x.ExecutionStartTimeList, startAt)
                    .Inc(x => x.ExecutionCount, 1)
            );

        }

        public void Completed(Guid commandId, DateTime completedAt)
        {
            var id = commandId.ToString();
            if (JarvisFrameworkGlobalConfiguration.MetricsEnabled)
            {
                var completed = _commands.FindAndModify(
                    Query<TrackedMessageModel>.EQ(x => x.MessageId, id),
                    SortBy<TrackedMessageModel>.Ascending(x => x.MessageId),
                    Update<TrackedMessageModel>.Set(x => x.CompletedAt, completedAt),
                    true
                );
                if (completed.Ok)
                {
                    var tracking = completed.ModifiedDocument;
                    TrackedMessageModel trackMessage = BsonSerializer.Deserialize<TrackedMessageModel>(tracking);
                    if (trackMessage.StartedAt > DateTime.MinValue)
                    {
                        var firstExecutionValue = trackMessage.ExecutionStartTimeList[0];
                        var queueTime = firstExecutionValue.Subtract(trackMessage.StartedAt).TotalMilliseconds;
                        var messageType = trackMessage.Message.GetType().Name;
                        queueTimer.Record((Int64) queueTime, TimeUnit.Milliseconds, messageType);
                        queueCounter.Increment(messageType, (Int64)queueTime);

                        var executionTime = completedAt.Subtract(trackMessage.StartedAt);
                        totalExecutionTimer.Record((Int64)queueTime, TimeUnit.Milliseconds, messageType);
                        totalExecutionCounter.Increment(messageType, (Int64)queueTime);
                    }
                }
            }
            else
            {
                _commands.Update(
                    Query<TrackedMessageModel>.EQ(x => x.MessageId, id),
                    Update<TrackedMessageModel>.Set(x => x.CompletedAt, completedAt),
                    UpdateFlags.Upsert
                );
            }
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
            errorMeter.Mark();
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

        public void ElaborationStarted(Guid commandId, DateTime startAt)
        {

        }
    }
}

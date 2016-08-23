using System;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson;
using MongoDB.Driver;
using Metrics;
using MongoDB.Bson.Serialization;
using System.Linq;
using Jarvis.Framework.Shared.Helpers;
using Castle.Core.Logging;

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
        private IMongoCollection<TrackedMessageModel> _commands;
        IMongoCollection<TrackedMessageModel> Commands
        {
            get
            {
                return _commands ?? (_commands = GetCollection());
            }
        }

        private static readonly Timer queueTimer = Metric.Timer("CommandWaitInQueue", Unit.Commands);
        private static readonly Timer totalExecutionTimer = Metric.Timer("CommandTotalExecution", Unit.Commands);
        private static readonly Counter queueCounter = Metric.Counter("CommandsWaitInQueue", Unit.Custom("ms"));
        private static readonly Counter totalExecutionCounter = Metric.Counter("CommandsTotalExecution", Unit.Custom("ms"));

        private static readonly Meter errorMeter = Metric.Meter("CommandFailures", Unit.Commands);

        //private static readonly Counter retryCount = Metric.Counter("CommandsRetry", Unit.Custom("ms"));

        public ILogger Logger { get; set; }

        private IMongoDatabase _db;

        private Boolean _initialized = false;

        public MongoDbMessagesTracker(IMongoDatabase db)
        {
            _db = db;
            Logger = NullLogger.Instance;
        }

        private IMongoCollection<TrackedMessageModel> GetCollection()
        {

            var state = _db.Client.Cluster.Description.State;
            //Check if db is operational.
            if (state == MongoDB.Driver.Core.Clusters.ClusterState.Connected)
            {
                var collection = _db.GetCollection<TrackedMessageModel>("messages");
                collection.Indexes.CreateOne(Builders<TrackedMessageModel>.IndexKeys.Ascending(x => x.MessageId));
                collection.Indexes.CreateOne(Builders<TrackedMessageModel>.IndexKeys.Ascending(x => x.IssuedBy));
                return collection;
            }
            return null;
        }

        public void Started(IMessage msg)
        {
            try
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

                Commands.UpdateOne(
                   Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                   Builders<TrackedMessageModel>.Update
                        .Set(x => x.Message, msg)
                        .Set(x => x.StartedAt, DateTime.UtcNow)
                        .Set(x => x.IssuedBy, issuedBy)
                        .Set(x => x.Description, msg.Describe()),
                   new UpdateOptions() { IsUpsert = true }
                );
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to track Started event of Message {0} [{1}] - {2}", msg.Describe(), msg.MessageId, ex.Message);
            }

        }

        public void ElaborationStarted(Guid commandId, DateTime startAt)
        {
            try
            {
                var id = commandId.ToString();
                var updated = Commands.UpdateOne(
                    Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                     Builders<TrackedMessageModel>.Update
                        .Set(x => x.LastExecutionStartTime, startAt)
                        .Push(x => x.ExecutionStartTimeList, startAt)
                        .Inc(x => x.ExecutionCount, 1)
                );
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to track ElaborationStarted event of Message {0} - {1}", commandId, ex.Message);
            }


        }

        public void Completed(Guid commandId, DateTime completedAt)
        {
            try
            {
                var id = commandId.ToString();
                if (JarvisFrameworkGlobalConfiguration.MetricsEnabled)
                {
                    var trackMessage = Commands.FindOneAndUpdate(
                        Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                        Builders<TrackedMessageModel>.Update.Set(x => x.CompletedAt, completedAt),
                        new FindOneAndUpdateOptions<TrackedMessageModel, TrackedMessageModel>()
                        {
                            IsUpsert = true,
                            ReturnDocument = ReturnDocument.After
                        }
                    );
                    if (trackMessage != null)
                    {
                        if (trackMessage.StartedAt > DateTime.MinValue)
                        {
                            if (trackMessage.ExecutionStartTimeList != null &&
                                trackMessage.ExecutionStartTimeList.Length > 0)
                            {
                                var firstExecutionValue = trackMessage.ExecutionStartTimeList[0];
                                var queueTime = firstExecutionValue.Subtract(trackMessage.StartedAt).TotalMilliseconds;

                                var messageType = trackMessage.Message.GetType().Name;
                                queueTimer.Record((Int64)queueTime, TimeUnit.Milliseconds, messageType);
                                queueCounter.Increment(messageType, (Int64)queueTime);

                                var executionTime = completedAt.Subtract(trackMessage.StartedAt);
                                totalExecutionTimer.Record((Int64)queueTime, TimeUnit.Milliseconds, messageType);
                                totalExecutionCounter.Increment(messageType, (Int64)queueTime);
                            }
                            else
                            {
                                Logger.WarnFormat("Command id {0} received completed event but ExecutionStartTimeList is empty", commandId);
                            }
                        }
                    }
                }
                else
                {
                    Commands.UpdateOne(
                         Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                         Builders<TrackedMessageModel>.Update.Set(x => x.CompletedAt, completedAt),
                         new UpdateOptions() { IsUpsert = true }
                     );
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to track Completed event of Message {0} - {1}", commandId, ex.Message);
            }
        }

        public bool Dispatched(Guid commandId, DateTime dispatchedAt)
        {
            try
            {
                var id = commandId.ToString();
                var result = Commands.UpdateOne(
                     Builders<TrackedMessageModel>.Filter.And(
                        Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                         Builders<TrackedMessageModel>.Filter.Eq(x => x.DispatchedAt, null)
                    ),
                    Builders<TrackedMessageModel>.Update.Set(x => x.DispatchedAt, dispatchedAt)
                );

                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to track Dispatched event of Message {0} - {1}", commandId, ex.Message);
            }
            return false;
        }

        public void Drop()
        {
            Commands.Drop();
        }

        public void Failed(Guid commandId, DateTime failedAt, Exception ex)
        {
            try
            {
                var id = commandId.ToString();
                Commands.UpdateOne(
                    Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                    Builders<TrackedMessageModel>.Update
                        .Set(x => x.FailedAt, failedAt)
                        .Set(x => x.ErrorMessage, ex.Message),
                    new UpdateOptions() { IsUpsert = true }
                );
                errorMeter.Mark();
            }
            catch (Exception iex)
            {
                Logger.ErrorFormat(iex, "Unable to track Failed event of Message {0} - {1}", commandId, ex.Message);
            }
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

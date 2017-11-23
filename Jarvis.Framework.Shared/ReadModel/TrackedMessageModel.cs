using System;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson;
using MongoDB.Driver;
using Metrics;
using Jarvis.Framework.Shared.Helpers;
using Castle.Core.Logging;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IMessagesTracker
    {

        /// <summary>
        /// A message (Command, Event, Something else) was sent to the bus,
        /// this is the first event that is raised.
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
        /// <param name="command"></param>
        /// <param name="startAt"></param>
        void ElaborationStarted(ICommand command, DateTime startAt);

        /// <summary>
        /// Message was elaborated with success
        /// </summary>
        /// <param name="command"></param>
        /// <param name="completedAt"></param>
        void Completed(ICommand command, DateTime completedAt);

		/// <summary>
		/// Dispatched is the status when the event related to the command is 
		/// dispatched by the <see cref="Jarvis.Framework.Kernel.ProjectionEngine.Client.INotifyCommitHandled"/> in projection engine. This means
		/// that the command is executed then dispatched to the bus and if there
		/// is a Reply-to a reply command is sent.
		/// </summary>
		/// <param name="messageId"></param>
		/// <param name="dispatchedAt"></param>
		/// <returns></returns>
		bool Dispatched(Guid messageId, DateTime dispatchedAt);

        /// <summary>
        /// Drop the entire collection.
        /// </summary>
        void Drop();

        /// <summary>
        /// Message cannot be elaborated, some error prevents the message to be
        /// handled.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="failedAt"></param>
        /// <param name="ex"></param>
        void Failed(ICommand command, DateTime failedAt, Exception ex);
    }

    public interface IMessagesTrackerQueryManager
    {
        List<TrackedMessageModel> GetByIdList(List<String> idList);

    }

    public enum TrackedMessageType
    {
        Unknown = 0,
        Command = 1,
        Event = 2
    }

    public class TrackedMessageModel
    {
        public ObjectId Id { get; set; }

        public string MessageId { get; set; }

        /// <summary>
        /// Identifies the Type of message (Command, Event, etc... for easier queries)
        /// 
        /// It's nullable because this field was added at a later time
        /// </summary>
        public TrackedMessageType? Type { get; set; }

        /// <summary>
        /// the type of the message in string format
        /// </summary>
        public String MessageType { get; set; }

        /// <summary>
        /// Timestamp when message is "started", with bus it is the time the message is sent to the bus
        /// this is the timestamp the message is generated.
        /// 
        /// This information is valid for:
        /// - Commands
        /// - Events
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// This is an array because the command can have retries, due to conflicts. This property stores
        /// all the execution start time for the command
        /// 
        /// This information is valid for:
        /// - Commands
        /// </summary>
        public DateTime[] ExecutionStartTimeList { get; set; }

        /// <summary>
        /// Last execution start time. 
        /// 
        /// This information is valid for:
        /// - Commands
        /// </summary>
        public DateTime? LastExecutionStartTime { get; set; }

        /// <summary>
        /// Set when the elaboration start, a command can then:
        /// - complete with success (when CompletedAt is set)
        /// - complete with a failure (when FailedAt is set)
        /// - pending: if this is set but this is not marked as completed or failed
        /// 
        /// In case of retries, this value is greater than 1 
        /// 
        /// This information is valid for:
        /// - Commands
        /// </summary>
        public Int32 ExecutionCount { get; set; }

        /// <summary>
        /// Time of completion of the command.
        /// 
        /// This information is valid for:
        /// - Commands
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Time of final dispatch of the command, this is the last message.
        /// 
        /// This information is valid for:
        /// - Commands
        /// </summary>
        public DateTime? DispatchedAt { get; set; }

        ///// <summary>
        ///// Timestamp of failure if the command cannot be executed.
        ///// </summary>
        //public DateTime? FailedAt { get; set; }

        public IMessage Message { get; set; }

        public string Description { get; set; }

        public string IssuedBy { get; set; }

        /// <summary>
        /// most recent error
        /// 
        /// This information is valid for:
        /// - Commands
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// True when the command is completed.
        /// 
        /// This information is valid for:
        /// - Commands
        /// </summary>
        public Boolean? Completed { get; set; }

        /// <summary>
        /// True if the command completed successfully.
        /// 
        /// This information is valid for:
        /// - Commands
        /// </summary>
        public Boolean? Success { get; set; }
    }

    public class MongoDbMessagesTracker : IMessagesTracker, IMessagesTrackerQueryManager
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
                TrackedMessageType type = TrackedMessageType.Unknown;

                if (msg is ICommand)
                {
                    type = TrackedMessageType.Command;
                    issuedBy = ((ICommand)msg).GetContextData(MessagesConstants.UserId);
                }
                else if (msg is IDomainEvent)
                {
                    type = TrackedMessageType.Event;
                    issuedBy = ((IDomainEvent)msg).IssuedBy;
                }

                Commands.UpdateOne(
                   Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                   Builders<TrackedMessageModel>.Update
                        .Set(x => x.Message, msg)
                        .Set(x => x.StartedAt, DateTime.UtcNow)
                        .Set(x => x.IssuedBy, issuedBy)
                        .Set(x => x.Description, msg.Describe())
                        .Set(x => x.Type, type)
                        .Set(x => x.MessageType, msg.GetType().Name),
                   new UpdateOptions() { IsUpsert = true }
                );
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to track Started event of Message {0} [{1}] - {2}", msg.Describe(), msg.MessageId, ex.Message);
            }
        }

        public void ElaborationStarted(ICommand command, DateTime startAt)
        {
            try
            {
                var id = command.MessageId.ToString();
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
                Logger.ErrorFormat(ex, "Unable to track ElaborationStarted event of Message {0} - {1}", command.MessageId, ex.Message);
            }
        }

        public void Completed(ICommand command, DateTime completedAt)
        {
            try
            {
                var id = command.MessageId.ToString();
                var mongoUpdate = Builders<TrackedMessageModel>.Update
                    .Set(x => x.CompletedAt, completedAt)
                    //.Set(x => x.FailedAt, null)
                    .Set(x => x.ErrorMessage, null)
                    .Set(x => x.Completed, true)
                    .Set(x => x.Success, true);
                var equalityCheck = Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id);
                if (JarvisFrameworkGlobalConfiguration.MetricsEnabled)
                {
                    var trackMessage = Commands.FindOneAndUpdate(
                        equalityCheck,
                        mongoUpdate,
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
                                Logger.WarnFormat("Command id {0} received completed event but ExecutionStartTimeList is empty", command.MessageId);
                            }
                        }
                    }
                }
                else
                {
                    //track completed date, delete all error messages.
                    Commands.UpdateOne(
                         equalityCheck,
                         mongoUpdate,
                         new UpdateOptions() { IsUpsert = true }
                     );
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to track Completed event of Message {0} - {1}", command.MessageId, ex.Message);
            }
        }

        public bool Dispatched(Guid messageId, DateTime dispatchedAt)
        {
            try
            {
                var id = messageId.ToString();
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
                Logger.ErrorFormat(ex, "Unable to track Dispatched event of Message {0} - {1}", messageId, ex.Message);
            }
            return false;
        }

        public void Drop()
        {
            Commands.Drop();
        }

        public void Failed(ICommand command, DateTime failedAt, Exception ex)
        {
            try
            {
                var id = command.MessageId.ToString();
                Commands.UpdateOne(
                    Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                    Builders<TrackedMessageModel>.Update
                        //.Set(x => x.FailedAt, failedAt)
                        .Set(x => x.CompletedAt, failedAt)
                        .Set(x => x.ErrorMessage, ex.Message)
                        .Set(x => x.Success, false)
                        .Set(x => x.Completed, true),
                    new UpdateOptions() { IsUpsert = true }
                );
                errorMeter.Mark();
            }
            catch (Exception iex)
            {
                Logger.ErrorFormat(iex, "Unable to track Failed event of Message {0} - {1}", command.MessageId, ex.Message);
            }
        }

        public List<TrackedMessageModel> GetByIdList(List<string> idList)
        {
            return Commands.Find(
                Builders<TrackedMessageModel>.Filter.In(m => m.MessageId, idList))
                .ToList();
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

        public void Completed(ICommand command, DateTime completedAt)
        {

        }

        public bool Dispatched(Guid messageId, DateTime dispatchedAt)
        {
            return true;
        }

        public void Drop()
        {

        }

        public void Failed(ICommand command, DateTime failedAt, Exception ex)
        {

        }

        public void ElaborationStarted(ICommand command, DateTime startAt)
        {

        }
    }
}

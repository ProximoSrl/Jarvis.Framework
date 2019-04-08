using Castle.Core.Logging;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using Metrics;
using MongoDB.Driver;
using NStore.Core.Streams;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Shared.Commands.Tracking
{
    public class MongoDbMessagesTracker : IMessagesTracker, IMessagesTrackerQueryManager
    {
        private IMongoCollection<TrackedMessageModel> _commands;

        private IMongoCollection<TrackedMessageModel> Commands
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

        public ILogger Logger { get; set; }

        private readonly IMongoDatabase _db;

        public MongoDbMessagesTracker(IMongoDatabase db)
        {
            _db = db;
            Logger = NullLogger.Instance;
        }

        private IMongoCollection<TrackedMessageModel> GetCollection()
        {
            if (!MongoDriverHelper.CheckConnection(_db.Client))
                return null;

            var collection = _db.GetCollection<TrackedMessageModel>("messages");

            //Drop old index of version <= 4.x
            collection.Indexes.DropOneAsync("MessageId_1");
            collection.Indexes.DropOneAsync("IssuedBy_1");

            //Add new indexes of version > 4.x
            collection.Indexes.CreateMany(
                new[] {
                  new CreateIndexModel<TrackedMessageModel>(
                    Builders<TrackedMessageModel>.IndexKeys
                        .Ascending(m => m.MessageId),
                    new CreateIndexOptions()
                    {
                        Name = "MessageId",
                        Background = true
                    }),
                new CreateIndexModel<TrackedMessageModel>(
                    Builders<TrackedMessageModel>.IndexKeys
                        .Ascending(m => m.IssuedBy)
                        .Ascending(m => m.StartedAt), //needed for sorting
                    new CreateIndexOptions()
                    {
                        Name = "IssuedBy",
                        Background = true
                    }
                ),
                 new CreateIndexModel<TrackedMessageModel>(
                    Builders<TrackedMessageModel>.IndexKeys
                        .Ascending(m => m.Type),
                    new CreateIndexOptions()
                    {
                        Name = "Type",
                        Background = true
                    }
                ),
                new CreateIndexModel<TrackedMessageModel>(
                    Builders<TrackedMessageModel>.IndexKeys
                        .Ascending(m => m.AggregateId)
                        .Ascending(m => m.Completed)
                        .Ascending(m => m.Success),
                    new CreateIndexOptions()
                    {
                        Name = "AggregateStatus",
                        Background = true
                    }
                )
            });
            return collection;
        }

        public void Started(IMessage msg)
        {
            try
            {
                var id = msg.MessageId.ToString();
                string issuedBy = null;
                TrackedMessageType type = TrackedMessageType.Unknown;
                String aggregateId = null;
                if (msg is ICommand cmd)
                {
                    type = TrackedMessageType.Command;
                    issuedBy = cmd.GetContextData(MessagesConstants.UserId);
                    aggregateId = cmd.ExtractAggregateId();
                }
                else if (msg is IDomainEvent de)
                {
                    type = TrackedMessageType.Event;
                    issuedBy = de.IssuedBy;
                }

                Commands.UpdateOne(
                   Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                   Builders<TrackedMessageModel>.Update
                        .Set(x => x.Message, msg)
                        .Set(x => x.AggregateId, aggregateId)
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
                Commands.UpdateOne(
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
                            if (trackMessage.ExecutionStartTimeList != null
                                && trackMessage.ExecutionStartTimeList.Length > 0)
                            {
                                var firstExecutionValue = trackMessage.ExecutionStartTimeList[0];
                                var queueTime = firstExecutionValue.Subtract(trackMessage.StartedAt).TotalMilliseconds;

                                var messageType = trackMessage.Message.GetType().Name;
                                queueTimer.Record((Int64)queueTime, TimeUnit.Milliseconds, messageType);
                                queueCounter.Increment(messageType, (Int64)queueTime);

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
            Commands?.Drop();
        }

        public void Failed(ICommand command, DateTime failedAt, Exception ex)
        {
            LogTypedException(command, ex);
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
                Logger.ErrorFormat(iex, "Unable to track Failed event of Message {0} - {1}", command?.MessageId, ex?.Message);
            }
        }

        private void LogTypedException(ICommand command, Exception ex)
        {
            var exception = ex?.ExtractException();
            //We reached the maximum number of retry, the execution was not successful.
            if (exception is ConcurrencyException)
            {
                Logger.ErrorFormat(exception, "Too many conflict on command {0} [MessageId: {1}] : {2}", command.GetType(), command.MessageId, command.Describe());
            }
            else if (exception is DomainException)
            {
                Logger.ErrorFormat(exception, "Domain exception on command {0} [MessageId: {1}] : {2}", command.GetType(), command.MessageId, command.Describe());
            }
        }

        #region Queries

        public List<TrackedMessageModel> GetByIdList(List<string> idList)
        {
            return Commands.Find(
                Builders<TrackedMessageModel>.Filter.In(m => m.MessageId, idList))
                .ToList();
        }

        /// <summary>
        /// Get a list of command for a user with pagination.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public TrackedMessageModelPaginated GetCommands(string userId, int pageIndex, int pageSize)
        {
            var query = Commands.AsQueryable()
              .Where(m =>
                    m.IssuedBy == userId
                    && m.Type == TrackedMessageType.Command);

            var countOfResults = query.Count();
            var totalPages = (int)Math.Ceiling((double)countOfResults / pageSize);

            var pagedQuery = query.OrderByDescending(m => m.StartedAt)
               .Skip((pageIndex - 1) * pageSize)
               .Take(pageSize);

            return new TrackedMessageModelPaginated
            {
                TotalPages = totalPages,
                Commands = pagedQuery.ToArray()
            };
        }

        public List<TrackedMessageModel> Query(MessageTrackerQuery query, int limit)
        {
            if (limit > 1000)
            {
                limit = 1000;
            }
            var queryable = query
                .ComposeLinqQuery(Commands.AsQueryable())
                .Take(limit);
            return queryable.ToList();
        }

        #endregion
    }
}

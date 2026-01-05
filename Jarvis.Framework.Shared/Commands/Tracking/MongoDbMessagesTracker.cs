using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Meter;
using App.Metrics.Timer;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Support;
using MongoDB.Driver;
using NStore.Core.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Commands.Tracking
{
    /// <summary>
    /// Track progress of command inside mongo database (usually the log database)
    /// </summary>
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

        private static readonly TimerOptions QueueTimer = new TimerOptions
        {
            Name = "CommandWaitInQueue",
            MeasurementUnit = Unit.Requests,
            DurationUnit = TimeUnit.Milliseconds,
            RateUnit = TimeUnit.Milliseconds
        };

        private static readonly CounterOptions QueueCounter = new CounterOptions()
        {
            Name = "CommandsWaitInQueue",
            MeasurementUnit = Unit.Commands
        };

        private static readonly MeterOptions ErrorMeter = new MeterOptions
        {
            Name = "CommandFailures",
            MeasurementUnit = Unit.Commands
        };

        public ILogger Logger { get; set; }

        private readonly IMongoDatabase _db;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="db"></param>
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

            var indexList = collection.GetIndexNames();

            //Drop old index of version <= 4.x
            if (indexList.Any(n => n.Equals("MessageId_1", StringComparison.OrdinalIgnoreCase)))
            {
                collection.Indexes.DropOneAsync("MessageId_1");
            }
            if (indexList.Any(n => n.Equals("IssuedBy_1", StringComparison.OrdinalIgnoreCase)))
            {
                collection.Indexes.DropOneAsync("IssuedBy_1");
            }

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

            //Create time to live index
            collection.Indexes.CreateOne(
                  new CreateIndexModel<TrackedMessageModel>(
                    Builders<TrackedMessageModel>.IndexKeys
                        .Ascending(m => m.ExpireDate),
                    new CreateIndexOptions()
                    {
                        Name = "ExpireDate",
                        Background = true,
                        ExpireAfter = TimeSpan.Zero,
                    }));
            return collection;
        }

        /// <inheritdoc/>
        public void Started(IMessage msg)
        {
            try
            {
                var id = msg.MessageId.ToString();
                string issuedBy = null;
                TrackedMessageType type = TrackedMessageType.Unknown;
                String aggregateId = null;

                //basically I want stuff to go away in one week
                DateTime? expireDate = DateTime.UtcNow.AddDays(7);
                if (msg is ICommand cmd)
                {
                    type = TrackedMessageType.Command;
                    issuedBy = cmd.GetContextData(MessagesConstants.UserId);
                    aggregateId = cmd.ExtractAggregateId();
                    //ok this is a command we need to disable expire date for now, we will set the
                    //exact value when we will know if the command execution is ok or not
                    expireDate = null;
                }
                else if (msg is IDomainEvent de)
                {
                    type = TrackedMessageType.Event;
                    issuedBy = de.IssuedBy;
                }
                else if (msg is CommandHandled)
                {
                    type = TrackedMessageType.CommandHandled;
                }

                if (type != TrackedMessageType.Command
                    && type != TrackedMessageType.CommandHandled
                    && JarvisFrameworkGlobalConfiguration.DisableJarvisLogForNonCommandType)
                {
                    return;
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
                        .Set(x => x.MessageType, msg.GetType().Name)
                        .Set(x => x.Completed, false)
                        .Set(x => x.ExpireDate, expireDate),
                   new UpdateOptions() { IsUpsert = true }
                );
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to track Started event of Message {0} [{1}] - {2}", msg.Describe(), msg.MessageId, ex.Message);
            }
        }

        /// <inheritdoc/>
        public void ElaborationStarted(ICommand command, DateTime startAt)
        {
            try
            {
                var id = command.MessageId.ToString();
                Commands.UpdateOne(
                    Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                     Builders<TrackedMessageModel>.Update
                        .Set(x => x.LastExecutionStartTime, startAt)
                        .PushEach(
                            s => s.ExecutionStartTimeList,
                            new[] { startAt },
                            slice: -10)
                        .Inc(x => x.ExecutionCount, 1)
                );
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to track ElaborationStarted event of Message {0} - {1}", command.MessageId, ex.Message);
            }
        }

        /// <inheritdoc/>
        public void Completed(ICommand command, DateTime completedAt)
        {
            try
            {
                //command completed successfully, it is nice to have log remain for longer time, remember that we have all commands
                //in header of generated commit, so this log is somewhat redundant
                DateTime expireDate = DateTime.UtcNow.AddDays(30);

                var id = command.MessageId.ToString();
                var mongoUpdate = Builders<TrackedMessageModel>.Update
                    .Set(x => x.CompletedAt, completedAt)
                    //.Set(x => x.FailedAt, null)
                    .Set(x => x.ErrorMessage, null)
                    .Set(x => x.Completed, true)
                    .Set(x => x.Success, true)
                    .Set(x => x.ExpireDate, expireDate);
                var equalityCheck = Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id);

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
                        if (trackMessage.ExecutionStartTimeList?.Length > 0)
                        {
                            var firstExecutionValue = trackMessage.ExecutionStartTimeList[0];
                            var queueTime = firstExecutionValue.Subtract(trackMessage.StartedAt).TotalMilliseconds;

                            var messageType = trackMessage.Message.GetType().Name;
                            JarvisFrameworkMetricsHelper.Timer.Time(QueueTimer, (long)queueTime);
                            JarvisFrameworkMetricsHelper.Counter.Increment(QueueCounter, (Int64)queueTime, messageType);
                        }
                        else
                        {
                            Logger.WarnFormat("Command id {0} received completed event but ExecutionStartTimeList is empty", command.MessageId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to track Completed event of Message {0} - {1}", command.MessageId, ex.Message);
            }
        }

        /// <summary>
        /// Track a batch of commands as executed instantaneously.
        /// </summary>
        /// <param name="commands">Commands to mark as executed.</param>
        /// <param name="failedCommands">Optional list of failed commands with error details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task TrackBatchAsync(IReadOnlyCollection<ICommand> commands, IReadOnlyCollection<FailedCommandInfo> failedCommands = null, CancellationToken cancellationToken = default)
        {
            if (commands == null || commands.Count == 0) return;
            try
            {
                var now = DateTime.UtcNow;
                var failedMap = (failedCommands ?? Array.Empty<FailedCommandInfo>())
                    .Where(f => f?.Command != null)
                    .ToDictionary(f => f.Command.MessageId.ToString(), f => f);

                var updates = new List<WriteModel<TrackedMessageModel>>();
                foreach (var command in commands)
                {
                    var id = command.MessageId.ToString();
                    var issuedBy = command.GetContextData(MessagesConstants.UserId);
                    var aggregateId = command.ExtractAggregateId();

                    var isFailed = failedMap.TryGetValue(id, out var failedInfo);

                    var update = Builders<TrackedMessageModel>.Update
                        .Set(x => x.Message, command)
                        .Set(x => x.MessageType, command.GetType().Name)
                        .Set(x => x.Description, command.Describe())
                        .Set(x => x.IssuedBy, issuedBy)
                        .Set(x => x.AggregateId, aggregateId)
                        .Set(x => x.CompletedAt, now)
                        .Set(x => x.LastExecutionStartTime, now)
                        .Set(x => x.ExecutionStartTimeList, new[] { now })
                        .Inc(x => x.ExecutionCount, 1)
                        .Set(x => x.Completed, true);

                    if (isFailed)
                    {
                        update = update
                            .Set(x => x.ErrorMessage, failedInfo.ErrorMessage)
                            .Set(x => x.FullException, failedInfo.Exception?.ToString())
                            .Set(x => x.Success, false)
                            .Set(x => x.ExpireDate, now.AddYears(7));
                    }
                    else
                    {
                        update = update
                            .Set(x => x.ErrorMessage, null)
                            .Set(x => x.FullException, null)
                            .Set(x => x.Success, true)
                            .Set(x => x.ExpireDate, now.AddDays(30));
                    }

                    var filter = Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id);
                    updates.Add(new UpdateOneModel<TrackedMessageModel>(filter, update) { IsUpsert = true });
                }

                await Commands.BulkWriteAsync(
                    updates,
                    new BulkWriteOptions { IsOrdered = false },
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var ids = commands.Select(c => c.MessageId.ToString()).ToList();
                var modifiedMessages = await Commands.Find(Builders<TrackedMessageModel>.Filter.In(m => m.MessageId, ids)).ToListAsync(cancellationToken).ConfigureAwait(false);


                foreach (var trackMessage in modifiedMessages)
                {
                    if (trackMessage.StartedAt > DateTime.MinValue)
                    {
                        if (trackMessage.ExecutionStartTimeList?.Length > 0)
                        {
                            var firstExecutionValue = trackMessage.ExecutionStartTimeList[0];
                            var queueTime = firstExecutionValue.Subtract(trackMessage.StartedAt).TotalMilliseconds;

                            var messageType = trackMessage.Message.GetType().Name;
                            JarvisFrameworkMetricsHelper.Timer.Time(QueueTimer, (long)queueTime);
                            JarvisFrameworkMetricsHelper.Counter.Increment(QueueCounter, (Int64)queueTime, messageType);
                        }
                        else
                        {
                            Logger.WarnFormat("Command id {0} received completed event but ExecutionStartTimeList is empty", trackMessage.MessageId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to track TrackBatchAsync - {0}", ex.Message);
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void Drop()
        {
            Commands?.Drop();
        }

        /// <inheritdoc />
        public void Failed(ICommand command, DateTime failedAt, Exception ex)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            LogTypedException(command, ex);
            try
            {
                if (ex is AggregateException aex)
                {
                    ex = aex.Flatten()?.InnerException;
                }

                //command failed, this is an important information we want to keep for really long time
                DateTime expireDate = DateTime.UtcNow.AddYears(7);

                var id = command.MessageId.ToString();
                Commands.UpdateOne(
                    Builders<TrackedMessageModel>.Filter.Eq(x => x.MessageId, id),
                    Builders<TrackedMessageModel>.Update
                        //.Set(x => x.FailedAt, failedAt)
                        .Set(x => x.CompletedAt, failedAt)
                        .Set(x => x.ErrorMessage, ex?.Message)
                        .Set(x => x.FullException, ex?.ToString())
                        .Set(x => x.Success, false)
                        .Set(x => x.Completed, true)
                        .Set(x => x.ExpireDate, expireDate),
                    new UpdateOptions() { IsUpsert = true }
                );
                JarvisFrameworkMetricsHelper.Meter.Mark(ErrorMeter);
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

        private static readonly SortDefinition<TrackedMessageModel> OrederByCreationDateDescending = Builders<TrackedMessageModel>.Sort.Descending(m => m.StartedAt);

        /// <inheritdoc/>
        public List<TrackedMessageModel> GetByIdList(IEnumerable<String> idList)
        {
            return Commands.Find(
                Builders<TrackedMessageModel>.Filter.In(m => m.MessageId, idList))
                .ToList();
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public List<TrackedMessageModel> Query(MessageTrackerQuery query, int limit)
        {
            if (limit > 1000)
            {
                limit = 1000;
            }
            var mongoQuery = query.CreateFilter();

            return Commands
                .Find(mongoQuery)
                .Sort(OrederByCreationDateDescending)
                .Limit(limit)
                .ToList();
        }

        #endregion
    }
}

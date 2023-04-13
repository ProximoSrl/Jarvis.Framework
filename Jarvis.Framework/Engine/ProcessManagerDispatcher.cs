using Castle.Core.Logging;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Driver;
using NStore.Core.Persistence;
using NStore.Domain;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Engine
{
    public class ProcessManagerDispatcher
    {
        private readonly ICommandBus _commandBus;

        public ILogger Logger { get; set; }

        private readonly IMongoCollection<ProcessManagerCheckpoint> _checkpointCollection;

        private readonly ProcessManagerCheckpoint _currentCheckpoint;

        public const String ProcessManagerId = "Jarvis.ProcessManager.Service";

        private readonly ICommitPollingClient _client;

        private readonly ProcessManagerConfiguration _configuration;

        private Boolean _started;
        private readonly IMessageBus _messageBus;

        public ProcessManagerDispatcher(
            ProcessManagerConfiguration configuration,
            IMongoDatabase supportDatabase,
            ICommitPollingClientFactory pollingClientFactory,
            IPersistence persistence,
            ICommandBus commandBus,
            IMessageBus messageBus)
        {
            _configuration = configuration;
            _commandBus = commandBus;
            _checkpointCollection = supportDatabase.GetCollection<ProcessManagerCheckpoint>("sysPmCheckpoints");
            _currentCheckpoint = _checkpointCollection.FindOneById(ProcessManagerId)
                ?? new ProcessManagerCheckpoint() { Id = ProcessManagerId, LastDispatchedPosition = 0 };
            Logger = NullLogger.Instance;

            _client = pollingClientFactory.Create(persistence, "ProcessManager");
            _client.AddConsumer("ProcessManager", Dispatch);
            _messageBus = messageBus;
        }

        public void Start(int frequenceInMilliseconds)
        {
            if (!_configuration.DispatchCommand) return;
            _client.Configure(_currentCheckpoint.LastDispatchedPosition, 100);
            _client.Start(frequenceInMilliseconds);
            _started = true;
        }

        public void Stop()
        {
            if (_started)
            {
                _client.Stop(false);
                _started = false;
            }
        }

        public Task Poll()
        {
            return _client.PollAsync();
        }

        public async Task Dispatch(IChunk commit)
        {
            var changeset = commit.Payload as Changeset;
            if (changeset != null)
            {
                var reactions = changeset.Events.OfType<MessageReaction>();
                foreach (var reaction in reactions)
                {
                    foreach (var message in reaction.MessagesOut)
                    {
                        try
                        {
                            if (message is IScheduledAt scheduledAt)
                            {
                                TimeSpan? at = null;

                                if (scheduledAt.At.ToUniversalTime() > DateTime.UtcNow)
                                {
                                    at = scheduledAt.At.ToUniversalTime().Subtract(DateTime.UtcNow);
                                }
                                await Dispatch(scheduledAt.Payload, at).ConfigureAwait(false);
                            }
                            else if (message is IMessageAndTimeout messageAndTimeout)
                            {
                                //1) send the message if needed
                                if (messageAndTimeout.SendMessageOut)
                                {
                                    await Dispatch(messageAndTimeout.Message, null).ConfigureAwait(false);
                                }

                                //2) send the timeout
                                String sagaId;
                                if (messageAndTimeout.SentToSelf)
                                {
                                    sagaId = commit.PartitionId;
                                }
                                else
                                {
                                    sagaId = messageAndTimeout.Target;
                                }
                                SagaTimeout timeout = new SagaTimeout(sagaId, GetCorrelationTimeoutKey(messageAndTimeout));
                                await Dispatch(timeout, messageAndTimeout.Delay).ConfigureAwait(false);
                            }
                            else
                            {
                                await Dispatch(message, null).ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.ErrorFormat(ex, "Error dispatching commit Id {0} Error {1}", commit.Position, ex.Message);
                        }
                    }
                }
            }

            _currentCheckpoint.LastDispatchedPosition = commit.Position;
            _checkpointCollection.Save(_currentCheckpoint, _currentCheckpoint.Id);
        }

        private string GetCorrelationTimeoutKey(IMessageAndTimeout messageAndTimeout)
        {
            if (messageAndTimeout.Message is IMessage)
            {
                return ((IMessage)messageAndTimeout.Message).MessageId.ToString();
            }
            return messageAndTimeout.Message.ToString();
        }

        private async Task Dispatch(Object payload, TimeSpan? delay)
        {
            if (payload is IMessage)
            {
                if (delay == null)
                {
                    await Dispatch((IMessage)payload).ConfigureAwait(false);
                }
                else
                {
                    await Dispatch((IMessage)payload, delay.Value).ConfigureAwait(false);
                }
            }
            else if (payload is ICommand)
            {
                if (delay == null)
                {
                    await Dispatch((ICommand)payload).ConfigureAwait(false);
                }
                else
                {
                    await Dispatch((ICommand)payload, delay.Value).ConfigureAwait(false);
                }
            }
            else
            {
                Logger.Error($"Unable to dispach object of type {payload.GetType()} it is neither a message nor a command");
            }
        }

        private Task Dispatch(IMessage message)
        {
            return _messageBus.SendAsync(message);
        }

        private Task Dispatch(IMessage message, TimeSpan delay)
        {
            return _messageBus.DeferAsync(delay, message);
        }

        private Task Dispatch(ICommand command)
        {
            return _commandBus.SendAsync(command);
        }

        private Task Dispatch(ICommand command, TimeSpan delay)
        {
            return _commandBus.DeferAsync(delay, command);
        }
    }

    public class ProcessManagerCheckpoint
    {
        /// <summary>
        /// Name of the dispatcher.
        /// </summary>
        public String Id { get; set; }

        /// <summary>
        /// Last dispatched checkpoint.
        /// </summary>
        public Int64 LastDispatchedPosition { get; set; }
    }

    public class ProcessManagerConfiguration
    {
        /// <summary>
        /// Default to true. It tells if ProcessManagerDispatcher is used to dispatch
        /// command.
        /// </summary>
        public Boolean DispatchCommand { get; protected set; }
        public string InternalAppUri { get; protected set; }

        protected ProcessManagerConfiguration()
        {
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using Jarvis.NEventStoreEx.Support;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Kernel.Events;

namespace Jarvis.Framework.Kernel.Commands
{
    public abstract class RepositoryCommandHandler<TAggregate, TCommand> : AbstractCommandHandler<TCommand>
        where TCommand : ICommand
        where TAggregate : class, IAggregateEx
    {
        /// <summary>
        /// This is the instance of the repository used to load/save aggregates
        /// </summary>
        public IRepositoryEx Repository { get; set; }

        /// <summary>
        /// When you use FindAndModify for a single aggregate (this is the most used pattern)
        /// it is better to use <see cref="IAggregateCachedRepository{TAggregate}"/> because
        /// it internally uses caches to cache the repository.
        /// </summary>
        public IAggregateCachedRepositoryFactory AggregateCachedRepositoryFactory { get; set; }

        // ReSharper restore MemberCanBeProtected.Global
        private Guid _commitId;

        public IConstructAggregatesEx AggregateFactory { get; set; }
        
        TCommand _currentCommand;

        /// <summary>
        /// This value is greater than -1 when the command has <see cref="MessagesConstants.IfVersionEqualsTo"/>
        /// header. Thus the caller requests to execute the command only if the aggregate is at a specific version.
        /// </summary>
        private Int32 _ifVersionEqualTo;

        /// <summary>
        /// Needed to check sync command execution.
        /// </summary>
        public IEventStoreQueryManager EventStoreQueryManager { get; set; }

        public override void Handle(TCommand cmd)
        {
            if (cmd.MessageId == Guid.Empty) 
            {
                Logger.ErrorFormat("Cmd {0} received with empty id.", cmd.GetType().Name);
                throw new Exception("Invalid command id");
            }

            _currentCommand = cmd;
            _commitId = cmd.MessageId;

            if (!Int32.TryParse(cmd.GetContextData(MessagesConstants.IfVersionEqualsTo), out _ifVersionEqualTo))
                _ifVersionEqualTo = -1; //no version info required.

            base.Handle(cmd);
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Handled command type {0} id {1} with handler {2}", cmd.GetType().Name, cmd.MessageId, this.GetType().Name);
        }

        /// <summary>
        /// This is a very special way to interact with an aggregate, it is the standard pattern
        /// where a command handler load an aggregate, invokes some methods on it then save.
        /// It can use cached instance of <see cref="IAggregateCachedRepository{TAggregate}"/> thus 
        /// achieving really faster performances.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <param name="createIfNotExists"></param>
        protected void FindAndModify(EventStoreIdentity id, Action<TAggregate> callback, bool createIfNotExists = false)
        {
            CheckAggregateForOfflinesync(id);
            if (JarvisFrameworkGlobalConfiguration.SingleAggregateRepositoryCacheEnabled)
            {
                //Lock is NEEDED because multiple thread cannot access the very same aggregate.
                lock (NamedLocker.Instance.GetLock(id.Id))
                {
                    using (var repo = AggregateCachedRepositoryFactory.Create<TAggregate>(id))
                    {
                        var aggregate = repo.Aggregate;

                        if (!createIfNotExists && aggregate.Version == 0)
                            throw new UninitializedAggregateException();

                        CheckAggregateVersionForIfVersionEqualTo(repo.Aggregate);

                        SetupContext(aggregate);
                        callback(aggregate);
                        repo.Save(_commitId, StoreCommandHeaders);
                    }
                }
            }
            else
            {
                //This is the classic command execution, each execution reload the entity and stream.
                var aggregate = Repository.GetById<TAggregate>(id);
                if (!createIfNotExists && aggregate.Version == 0)
                    throw new UninitializedAggregateException();

                CheckAggregateVersionForIfVersionEqualTo(aggregate);

                SetupContext(aggregate);
                callback(aggregate);
                Repository.Save(aggregate, _commitId, StoreCommandHeaders);
            }

        }

        private void CheckAggregateForOfflinesync(EventStoreIdentity id)
        {
            if (_currentCommand.GetContextData(MessagesConstants.SessionStartCheckEnabled) != null)
            {
                var checkpointTokenString = _currentCommand.GetContextData(MessagesConstants.SessionStartCheckpointToken);
                var checkpointToken = Int64.Parse(checkpointTokenString);
                var sessionId = _currentCommand.GetContextData(MessagesConstants.OfflineSessionId);

                var allCommitsForThisEntity = EventStoreQueryManager.GetCommitsAfterCheckpointToken(checkpointToken, new List<String>() { id.ToString() });
                foreach (var commit in allCommitsForThisEntity)
                {
                    //we have a conflicting commit, but the conflict occurred only if the headers OfflineSessionId is different from sessionId
                    if (!commit.Headers.ContainsKey(MessagesConstants.OfflineSessionId) ||
                        commit.Headers[MessagesConstants.OfflineSessionId] != sessionId)
                    {
                        throw new AggregateSyncConflictException(
                           String.Format("Command cannot be executed because header required that aggregate {0} does not have commits greater than checkpoint token {1} that does not belong to session {2}. Checkpoint {3} violates this rule", 
                                id, 
                                checkpointToken,
                                sessionId,
                                commit.CheckpointToken),
                           id,
                           checkpointToken,
                           sessionId);
                    }
                }
            }
        }

        private void CheckAggregateVersionForIfVersionEqualTo(IAggregateEx aggregate)
        {
            if (_ifVersionEqualTo > -1 && aggregate.Version != _ifVersionEqualTo)
                throw new AggregateModifiedException(
                    String.Format("Command cannot be executed because {0} header required aggregate at version {1} but actual aggregate version is {2}", MessagesConstants.IfVersionEqualsTo, _ifVersionEqualTo, aggregate.Version),
                    aggregate.Id.AsString(),
                    _ifVersionEqualTo,
                    aggregate.Version);
        }

        protected void Save(TAggregate aggregate)
        {
            Repository.Save(aggregate, _commitId, StoreCommandHeaders);
        }

        protected TAggregate CreateNewAggregate(IIdentity identity)
        {
            var aggregate = (TAggregate)AggregateFactory.Build(typeof(TAggregate), identity);
            SetupContext(aggregate);
            return aggregate;
        }

        protected void StoreCommandHeaders(IDictionary<string, object> headers)
        {
            foreach (var key in _currentCommand.AllContextKeys)
            {
                headers.Add(key, _currentCommand.GetContextData(key));
            }
        }

        protected void SetupContext(IAggregateEx aggregate)
        {
            var context = _currentCommand.AllContextKeys
                .ToDictionary<string, string, object>(
                    key => key, 
                    key => _currentCommand.GetContextData(key)
                );
            context["command.name"] = _currentCommand.GetType().Name;
            aggregate.EnterContext(context);
        }

    }
}
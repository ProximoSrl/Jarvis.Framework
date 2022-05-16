using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Persistence;
using Jarvis.Framework.Shared.Support;
using NStore.Domain;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Commands
{
    /// <summary>
    /// <para>Base command handler that is capable to restore an aggregate with standard <see cref="IRepository"/></para>
    /// <para>
    /// Standard way to interact with an aggregate is <see cref="FindAndModifyAsync(EventStoreIdentity, Action{TAggregate}, bool)"/>
    /// but command handler has the ability to interact directly with repository to interact with multiple aggregates. 
    /// This second method of implementing command handler is discouraged and it should be strictly idempotent.
    /// </para>
    /// </summary>
    /// <typeparam name="TAggregate"></typeparam>
    /// <typeparam name="TCommand"></typeparam>
    public abstract class RepositoryCommandHandler<TAggregate, TCommand> : AbstractCommandHandler<TCommand>
        where TAggregate : class, IAggregate
        where TCommand : ICommand
    {
        /// <summary>
        /// This is the instance of the repository used to load/save aggregates
        /// </summary>
        public IRepository Repository { get; set; }

        /// <summary>
        /// When you use FindAndModify for a single aggregate (this is the most used pattern)
        /// it is better to use <see cref="IAggregateCachedRepository{TAggregate}"/> because
        /// it internally uses caches to cache the repository.
        /// </summary>
        public IAggregateCachedRepositoryFactory AggregateCachedRepositoryFactory { get; set; }

        // ReSharper restore MemberCanBeProtected.Global
        private Guid _commitId;

        public IAggregateFactory AggregateFactory { get; set; }

        protected TCommand CurrentCommand { get; private set; }

        /// <summary>
        /// This value is greater than -1 when the command has <see cref="MessagesConstants.IfVersionEqualsTo"/>
        /// header. Thus the caller requests to execute the command only if the aggregate is at a specific version.
        /// </summary>
        private Int32 _ifVersionEqualTo;

        /// <summary>
        /// Needed to check sync command execution.
        /// </summary>
        public IEventStoreQueryManager EventStoreQueryManager { get; set; }

        public override Task ClearAsync()
        {
            Repository.Clear();
            return Task.CompletedTask;
        }

        public override async Task HandleAsync(TCommand cmd)
        {
            //Before handling any message, we want to be sure that the repository is clear.
            Repository.Clear();
            if (cmd.MessageId == Guid.Empty)
            {
                Logger.ErrorFormat("Cmd {0} received with empty id.", cmd.GetType().Name);
                throw new InvalidCommandException("Invalid command id");
            }

            CurrentCommand = cmd;
            _commitId = cmd.MessageId;

            if (!Int32.TryParse(cmd.GetContextData(MessagesConstants.IfVersionEqualsTo), out _ifVersionEqualTo))
            {
                _ifVersionEqualTo = -1; //no version info required.
            }

            await base.HandleAsync(cmd).ConfigureAwait(false);
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Handled command type {0} id {1} with handler {2}", cmd.GetType().Name, cmd.MessageId, this.GetType().Name);
            }
        }

        /// <summary>
        /// To maintain compatibility with the old command handler.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <param name="createIfNotExists"></param>
        /// <returns></returns>
        protected virtual Task FindAndModifyAsync(
            EventStoreIdentity id,
            Action<TAggregate> callback,
            bool createIfNotExists = false)
        {
            return FindAndModifyAsync(id, a =>
            {
                callback(a);
                return Task.FromResult(RepositoryCommandHandlerCallbackReturnValue.Default);
            },
            createIfNotExists);
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
        protected virtual async Task FindAndModifyAsync(
            EventStoreIdentity id,
            Func<TAggregate, Task<RepositoryCommandHandlerCallbackReturnValue>> callback,
            bool createIfNotExists = false)
        {
            if (JarvisFrameworkGlobalConfiguration.SingleAggregateRepositoryCacheEnabled)
            {
                //Lock is NEEDED because multiple thread cannot access the very same aggregate.
                lock (NamedLocker.Instance.GetLock(id.Id))
                {
                    using (var repo = AggregateCachedRepositoryFactory.Create<TAggregate>(id))
                    {
                        var aggregate = repo.Aggregate;

                        if (!createIfNotExists && aggregate.Version == 0)
                        {
                            throw new UninitializedAggregateException();
                        }

                        CheckAggregateVersionForIfVersionEqualTo(repo.Aggregate);

                        //TODO: Waiting is not a perfect solution, but we cannot await inside a lock.
                        var callbackResult = callback(aggregate).Result;
                        if (!callbackResult.ShouldNotPersistAggregate)
                        {
                            //TODO: Waiting is not a perfect solution, but we cannot await inside a lock.
                            repo.SaveAsync(_commitId, h => StoreCommandHeaders(h, CurrentCommand)).Wait();
                        }
                    }
                }
            }
            else
            {
                //This is the classic command execution, each execution reload the entity and stream.
                var aggregate = await Repository.GetByIdAsync<TAggregate>(id).ConfigureAwait(false);
                if (!createIfNotExists && aggregate.Version == 0)
                {
                    throw new UninitializedAggregateException();
                }

                CheckAggregateVersionForIfVersionEqualTo(aggregate);

                var callbackResult = await callback(aggregate).ConfigureAwait(false);

                if (!callbackResult.ShouldNotPersistAggregate)
                {
                    await Repository.SaveAsync(aggregate, _commitId.ToString(), h => StoreCommandHeaders(h, CurrentCommand)).ConfigureAwait(false);
                }
            }
        }

        private void CheckAggregateVersionForIfVersionEqualTo(IAggregate aggregate)
        {
            if (_ifVersionEqualTo > -1 && aggregate.Version != _ifVersionEqualTo)
            {
                throw new AggregateModifiedException(
                    String.Format("Command cannot be executed because {0} header required aggregate at version {1} but actual aggregate version is {2}", MessagesConstants.IfVersionEqualsTo, _ifVersionEqualTo, aggregate.Version),
                    aggregate.Id,
                    _ifVersionEqualTo,
                    aggregate.Version);
            }
        }

        protected Task SaveAsync(TAggregate aggregate)
        {
            return Repository.SaveAsync(aggregate, _commitId.ToString(), h => StoreCommandHeaders(h, CurrentCommand));
        }

        protected Task<TAggregate> CreateNewAggregateAsync(IIdentity identity)
        {
            return Repository.GetByIdAsync<TAggregate>(identity.AsString());
        }
    }

    /// <summary>
    /// This is the return value of the callback used in FindAndModify
    /// </summary>
    public class RepositoryCommandHandlerCallbackReturnValue
    {
        public static RepositoryCommandHandlerCallbackReturnValue Default { get; private set; } = new RepositoryCommandHandlerCallbackReturnValue();

        /// <summary>
        /// If false command handler should not persist aggregate
        /// </summary>
        public Boolean ShouldNotPersistAggregate { get; set; }
    }
}
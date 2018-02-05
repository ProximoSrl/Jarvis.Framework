using System;
using System.Collections.Generic;
using System.Linq;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Kernel.Events;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Persistence;
using Jarvis.Framework.Shared.Support;
using Jarvis.Framework.Shared.Events;
using NStore.Domain;

namespace Jarvis.Framework.Kernel.Commands
{
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

		private TCommand _currentCommand;

		/// <summary>
		/// This value is greater than -1 when the command has <see cref="MessagesConstants.IfVersionEqualsTo"/>
		/// header. Thus the caller requests to execute the command only if the aggregate is at a specific version.
		/// </summary>
		private Int32 _ifVersionEqualTo;

		/// <summary>
		/// Needed to check sync command execution.
		/// </summary>
		public IEventStoreQueryManager EventStoreQueryManager { get; set; }

		public override async Task HandleAsync(TCommand cmd)
		{
			//Before handling any message, we want to be sure that the repository is clear.
			Repository.Clear();
			if (cmd.MessageId == Guid.Empty)
			{
				Logger.ErrorFormat("Cmd {0} received with empty id.", cmd.GetType().Name);
				throw new InvalidCommandException("Invalid command id");
			}

			_currentCommand = cmd;
			_commitId = cmd.MessageId;

			if (!Int32.TryParse(cmd.GetContextData(MessagesConstants.IfVersionEqualsTo), out _ifVersionEqualTo))
				_ifVersionEqualTo = -1; //no version info required.

			await base.HandleAsync(cmd).ConfigureAwait(false);
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
		protected async Task FindAndModifyAsync(EventStoreIdentity id, Action<TAggregate> callback, bool createIfNotExists = false)
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
							throw new UninitializedAggregateException();

						CheckAggregateVersionForIfVersionEqualTo(repo.Aggregate);

						callback(aggregate);
						//TODO: Waiting is not a perfect solution, but we cannot await inside a lock.
						repo.SaveAsync(_commitId, StoreCommandHeaders).Wait();
					}
				}
			}
			else
			{
				//This is the classic command execution, each execution reload the entity and stream.
				var aggregate = await Repository.GetByIdAsync<TAggregate>(id).ConfigureAwait(false);
				if (!createIfNotExists && aggregate.Version == 0)
					throw new UninitializedAggregateException();

				CheckAggregateVersionForIfVersionEqualTo(aggregate);

				callback(aggregate);
				await Repository.SaveAsync(aggregate, _commitId.ToString(), StoreCommandHeaders).ConfigureAwait(false);
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
			return Repository.SaveAsync(aggregate, _commitId.ToString(), StoreCommandHeaders);
		}

		protected Task<TAggregate> CreateNewAggregateAsync(IIdentity identity)
		{
			return Repository.GetByIdAsync<TAggregate>(identity.AsString());
		}

		protected void StoreCommandHeaders(IHeadersAccessor headersAccessor)
		{
			foreach (var key in _currentCommand.AllContextKeys)
			{
				headersAccessor.Add(key, _currentCommand.GetContextData(key));
			}
			headersAccessor.Add(ChangesetCommonHeaders.Command, _currentCommand);
			headersAccessor.Add(ChangesetCommonHeaders.Timestamp, DateTime.UtcNow);
		}
	}
}
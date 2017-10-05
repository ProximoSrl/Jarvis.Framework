using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Commands
{
    /// <summary>
    /// It is useful to create an API controller that can execute commands
    /// from the web, this class is a general class that helps executing
    /// a command in process, and returns a dto that contains everything needed.
    /// This class also have the duty to grab conflicting commands for the
    /// offline system.
    /// </summary>
    public class CommandsExecutorHelper
    {
        private readonly IInProcessCommandBus _commandBus;
        private readonly IEventStoreQueryManager _eventStoreQueryManager;
        private readonly IMessagesTrackerQueryManager _messagesTrackerQueryManager;

        public IExtendedLogger Logger { get; set; }

        public CommandsExecutorHelper(
            IInProcessCommandBus commandBus,
            IEventStoreQueryManager eventStoreQueryManager,
            IMessagesTrackerQueryManager messagesTrackerQueryManager)
        {
            _commandBus = commandBus;
            _eventStoreQueryManager = eventStoreQueryManager;
            _messagesTrackerQueryManager = messagesTrackerQueryManager;
            Logger = NullLogger.Instance;
        }

        public async Task<ExecuteCommandResultDto> ExecuteAsync(ExecuteCommandDto dto)
        {
            var command = ExecuteCommandDto.Deserialize(dto);
			Logger.MarkCommandExecution(command);

			try
			{
				Logger.InfoFormat("Ask execution of command {0} - {1}", command.MessageId, command.Describe());

				var user = command.GetContextData(MessagesConstants.UserId);
				if (user == null)
					user = dto.ImpersonatingUser;

				if (user == null)
					throw new Exception($"Unable to execue a command, no user in header {MessagesConstants.UserId} nor impersonation user in dto is present.");

				try
				{
					await _commandBus.SendAsync(command, user).ConfigureAwait(false);
					return new ExecuteCommandResultDto(true, "", null);
				}
				catch (AggregateModifiedException ex)
				{
					//we have a conflicting exception
					var retValue = new ExecuteCommandResultDto(false, ex.ToString(), ex);
					if (dto.OfflineCheckpointTokenFrom.HasValue)
					{
						List<CommitShortInfo> newCommits = GetNewCommid(dto.OfflineCheckpointTokenFrom.Value, ex.AggregateId);
						var idList = newCommits.Select(c => c.CommitId.ToString()).ToList();
						retValue.ConflictingCommands = GetConflictingCommandList(ex.AggregateId, idList);
					}

					return retValue;
				}
				catch (AggregateSyncConflictException ex)
				{
					var retValue = new ExecuteCommandResultDto(false, ex.ToString(), ex);
					//TODO: use the new logic to find conflicting commands.
					List<CommitShortInfo> newCommits = GetNewCommid(ex.CheckpointToken, ex.AggregateId);
					String sessionId = ex.SessionGuidId;
					var idList = newCommits
						.Where(c => NotBelongToSession(c, sessionId))
						.Select(c => c.CommitId.ToString())
						.ToList();
					retValue.ConflictingCommands = GetConflictingCommandList(ex.AggregateId, idList);
					return retValue;
				}
				catch (Exception ex)
				{
					return new ExecuteCommandResultDto(false, ex.ToString(), ex);
				}
			}
			finally
			{
				Logger.ClearCommandExecution();
			}
        }

        /// <summary>
        /// Return True if the commit info does not belongs to this session.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        private bool NotBelongToSession(CommitShortInfo c, string sessionId)
        {
            return c.Headers.ContainsKey(MessagesConstants.OfflineSessionId) == false ||
                c.Headers[MessagesConstants.OfflineSessionId] != sessionId;
        }

        private List<CommitShortInfo> GetNewCommid(Int64 checkpointTokenFrom, String aggregateId)
        {
            return _eventStoreQueryManager.GetCommitsAfterCheckpointToken(
                checkpointTokenFrom,
                new List<String>() { aggregateId });
        }

        private List<ConflictingCommandInfo> GetConflictingCommandList(String aggregateId, List<string> idList)
        {
            var commandList = _messagesTrackerQueryManager.GetByIdList(idList);
            return commandList
                .Where(c => c.CompletedAt.HasValue)
                .Select(c => new ConflictingCommandInfo()
                {
                    AggregateId = aggregateId,
                    Describe = c.Message.Describe(),
                    Id = c.Message.MessageId.ToString(),
                    TimeStamp = c.CompletedAt.Value,
                    UserId = c.IssuedBy
                })
                .ToList();
        }
    }
}

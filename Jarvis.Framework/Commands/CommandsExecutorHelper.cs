using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Shared.Commands.Tracking;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public ILogger Logger { get; set; }

        public ILoggerThreadContextManager LoggerThreadContextManager { get; set; }

        public CommandsExecutorHelper(
            IInProcessCommandBus commandBus,
            IEventStoreQueryManager eventStoreQueryManager,
            IMessagesTrackerQueryManager messagesTrackerQueryManager)
        {
            _commandBus = commandBus;
            _eventStoreQueryManager = eventStoreQueryManager;
            _messagesTrackerQueryManager = messagesTrackerQueryManager;
            Logger = NullLogger.Instance;
            LoggerThreadContextManager = NullLoggerThreadContextManager.Instance;
        }

        public async Task<ExecuteCommandResultDto> ExecuteAsync(ExecuteCommandDto dto)
        {
            var command = ExecuteCommandDto.Deserialize(dto);
            LoggerThreadContextManager.MarkCommandExecution(command);
            Logger.InfoFormat("Ask execution of command {0} - {1}", command.MessageId, command.Describe());

            var user = (command.GetContextData(MessagesConstants.UserId) ?? dto.ImpersonatingUser) ?? throw new JarvisFrameworkEngineException($"Unable to execue a command, no user in header {MessagesConstants.UserId} nor impersonation user in dto is present.");
            try
            {
                await _commandBus.SendAsync(command, user).ConfigureAwait(false);
                return new ExecuteCommandResultDto(true, "", null);
            }
            catch (AggregateModifiedException ex)
            {
                //we have a conflicting exception
                return new ExecuteCommandResultDto(false, ex.Message, ex);
            }
            catch (Exception ex)
            {
                return new ExecuteCommandResultDto(false, ex.Message, ex);
            }
            finally
            {
                LoggerThreadContextManager.ClearMarkCommandExecution(); //clear data
            }
        }
    }
}

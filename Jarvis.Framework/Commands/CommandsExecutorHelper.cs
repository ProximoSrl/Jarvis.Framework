using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Messages;
using System;
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

        public ILogger Logger { get; set; }

        public ILoggerThreadContextManager LoggerThreadContextManager { get; set; }

        public CommandsExecutorHelper(
            IInProcessCommandBus commandBus)
        {
            _commandBus = commandBus;
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

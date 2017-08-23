using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Messages;
using Metrics;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Commands
{
	public abstract class AbstractCommandHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
	{
		public ILogger Logger { get; set; }

		public ILoggerThreadContextManager LoggerThreadContextManager { get; set; }

		protected AbstractCommandHandler()
		{
			Logger = NullLogger.Instance;
			LoggerThreadContextManager = NullLoggerThreadContextManager.Instance;
		}

		public virtual async Task HandleAsync(TCommand cmd)
		{
			if (JarvisFrameworkGlobalConfiguration.MetricsEnabled)
			{
				using (var context = MetricsHelper.CommandTimer.NewContext(cmd.GetType().Name))
				{
					await Execute(cmd).ConfigureAwait(false);
					MetricsHelper.CommandCounter.Increment(cmd.GetType().Name, context.Elapsed.Milliseconds);
				}
			}
			else
			{
				await Execute(cmd).ConfigureAwait(false);
			}
		}

		protected abstract Task Execute(TCommand cmd);

		public async Task HandleAsync(ICommand command)
		{
			using (LoggerThreadContextManager.MarkCommandExecution(command))
			{
				await HandleAsync((TCommand)command).ConfigureAwait(false);
			}
		}
	}
}
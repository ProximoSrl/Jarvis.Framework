using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Logging;
using Metrics;

namespace Jarvis.Framework.Kernel.Commands
{
    public abstract class AbstractCommandHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
    {
        public IExtendedLogger Logger { get; set; }

        protected AbstractCommandHandler()
        {
            Logger = NullLogger.Instance;
        }

        public virtual void Handle(TCommand cmd)
        {
            if (JarvisFrameworkGlobalConfiguration.MetricsEnabled)
            {
                using (var context = MetricsHelper.CommandTimer.NewContext(cmd.GetType().Name))
                {
                    Execute(cmd);
                    MetricsHelper.CommandCounter.Increment(cmd.GetType().Name, context.Elapsed.Milliseconds);
                }
            }
            else
            {
                Execute(cmd);
            }
        }

        protected abstract void Execute(TCommand cmd);

        public void Handle(ICommand command)
        {
            try
            {
                Logger.MarkCommandExecution(command);
                Handle((TCommand)command);
            }
            finally
            {
                Logger.ClearCommandExecution();
            }
        }
    }
}
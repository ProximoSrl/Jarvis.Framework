using Castle.Core.Logging;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Logging;
using Metrics;

namespace Jarvis.Framework.Kernel.Commands
{
    public abstract class AbstractCommandHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
    {
        private static readonly Timer timer = Metric.Timer("Commands Execution", Unit.Commands);
        private static readonly Counter commandCounter = Metric.Counter("CommandsDuration", Unit.Custom("ms"));

        public IExtendedLogger Logger { get; set; }

        public AbstractCommandHandler()
        {
            Logger = NullLogger.Instance;
        }

        public virtual void Handle(TCommand cmd)
        {
            if (JarvisFrameworkGlobalConfiguration.MetricsEnabled)
            {
                using (var context = timer.NewContext(cmd.GetType().Name))
                {
                    Execute(cmd);
                    commandCounter.Increment(cmd.GetType().Name, context.Elapsed.Milliseconds);
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
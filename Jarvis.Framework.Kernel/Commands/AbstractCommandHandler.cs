using Castle.Core.Logging;
using Jarvis.Framework.Shared.Commands;
using Metrics;

namespace Jarvis.Framework.Kernel.Commands
{
    public abstract class AbstractCommandHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
    {
        private static Timer timer = Metric.Timer("RawCommandExecution", Unit.Requests);

        public IExtendedLogger Logger { get; set; }

        public AbstractCommandHandler()
        {
            Logger = NullLogger.Instance;
        }

        public virtual void Handle(TCommand cmd)
        {
            using (timer.NewContext(cmd.GetType().Name))
            {
                Execute(cmd);
            }
        }

        protected abstract void Execute(TCommand cmd);

        public void Handle(ICommand command)
        {
            try
            {
                Logger.ThreadProperties["cmdid"] = command.MessageId;
                Logger.ThreadProperties["cmdtype"] = command.GetType();
                Handle((TCommand)command);
            }
            finally
            {
                Logger.ThreadProperties["commandId"] = null;
                Logger.ThreadProperties["cmdtype"] = null;
            }

        }
    }
}
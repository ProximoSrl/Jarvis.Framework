using System.Threading;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Shared.Commands;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.BusTests.Handlers
{
    public class SampleCommandHandler :
        ICommandHandler<SampleTestCommand>,
        ICommandHandler<SampleAggregateTestCommand>
    {
        public readonly ManualResetEvent Reset = new ManualResetEvent(false);

        public Task ClearAsync()
        {
            return Task.CompletedTask;
        }

        public Task HandleAsync(SampleTestCommand cmd)
        {
            this.ReceivedCommand = cmd;
            Reset.Set();
            return Task.CompletedTask;
        }

        public Task HandleAsync(SampleAggregateTestCommand cmd)
        {
            this.ReceivedAggregateCommand = cmd;
            Reset.Set();
            return Task.CompletedTask;
        }

        public Task HandleAsync(ICommand command)
        {
            return HandleAsync(command as SampleTestCommand);
        }

        public SampleTestCommand ReceivedCommand { get; private set; }
        public SampleAggregateTestCommand ReceivedAggregateCommand { get; private set; }
    }
}

using System.Threading;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Shared.Commands;

namespace Jarvis.Framework.Tests.BusTests.Handlers
{
    public class SampleCommandHandler : ICommandHandler<SampleTestCommand>
    {
        public readonly ManualResetEvent Reset = new ManualResetEvent(false);

        public void Handle(SampleTestCommand cmd)
        {
            this.ReceivedCommand = cmd;
            Reset.Set();
        }

        public void Handle(ICommand command)
        {
            Handle(command as SampleTestCommand);
        }

        public SampleTestCommand ReceivedCommand { get; private set; }
    }
}

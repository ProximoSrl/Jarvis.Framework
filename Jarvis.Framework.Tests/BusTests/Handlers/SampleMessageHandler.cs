using Jarvis.Framework.Tests.BusTests.MessageFolder;

namespace Jarvis.Framework.Tests.BusTests.Handlers
{
    public class SampleMessageHandler : IHandleMessages<SampleMessage>
    {
        public readonly ManualResetEvent Reset = new ManualResetEvent(false);

        public Task Handle(SampleMessage message)
        {
            this.ReceivedMessage = message;
            Reset.Set();
            return Task.CompletedTask;
        }

        public SampleMessage ReceivedMessage { get; private set; }
    }
}

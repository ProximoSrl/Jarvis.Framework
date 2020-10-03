using Jarvis.Framework.Tests.BusTests.MessageFolder;
using Rebus.Handlers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.BusTests.Handlers
{
    public class AnotherSampleMessageHandler : IHandleMessages<AnotherSampleMessage>
    {
        public static Int32 Seed = 0;

        public Int32 Id { get; private set; }

        public AnotherSampleMessageHandler()
        {
            Id = Interlocked.Increment(ref Seed);
        }

        public Int32 CallCount { get; set; }

        public Task Handle(AnotherSampleMessage message)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}

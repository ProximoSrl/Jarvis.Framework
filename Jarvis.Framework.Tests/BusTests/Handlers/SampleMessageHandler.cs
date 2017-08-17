using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using Rebus.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.BusTests.Handlers
{
    public class SampleMessageHandler : IHandleMessages<SampleMessage>
    {
        public readonly ManualResetEvent Reset = new ManualResetEvent(false);

        public Task Handle(SampleMessage message)
        {
            this.ReceivedMessage = message;
            Reset.Set();
            return TaskHelpers.CompletedTask;
        }

        public SampleMessage ReceivedMessage { get; private set; }
    }
}

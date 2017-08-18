using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using Rebus.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.BusTests.Handlers
{
    public class AnotherSampleMessageHandler : IHandleMessages<AnotherSampleMessage>
    {
        public Int32 CallCount { get; set; }

        public Task Handle(AnotherSampleMessage message)
        {
            CallCount++;
            return TaskHelpers.CompletedTask;
        }
    }
}

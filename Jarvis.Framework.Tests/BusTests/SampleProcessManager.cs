using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Tests.BusTests.MessageFolder;

namespace Jarvis.Framework.Tests.BusTests
{
    public class SampleProcessManager : AbstractProcessManager
        , IObserveMessage<SampleMessage>
    {
        public void On(SampleMessage message)
        {
            // Method intentionally left empty.
        }
    }
}
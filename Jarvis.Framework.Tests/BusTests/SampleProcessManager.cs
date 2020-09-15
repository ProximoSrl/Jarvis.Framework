using Jarvis.Framework.Tests.BusTests.MessageFolder;

namespace Jarvis.Framework.Tests.BusTests
{
    public class SampleProcessManager : AbstractProcessManager<SampleProcessManagerState>
    {
    }

    public class SampleProcessManagerState : AbstractProcessManagerState
    {
        public void On(SampleMessage message)
        {
            // Method intentionally left empty.
        }
    }
}
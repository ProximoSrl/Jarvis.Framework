using Jarvis.Framework.Kernel.Engine;

namespace Jarvis.Framework.Tests.BusTests
{
    public class SampleProcessManager : AbstractProcessManager
        , IObserveMessage<SampleMessage>
    {
        public void On(SampleMessage message)
        {
        }
    }
}
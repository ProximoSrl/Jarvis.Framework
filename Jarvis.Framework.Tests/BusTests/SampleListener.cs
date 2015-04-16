using Jarvis.Framework.Kernel.Engine;

namespace Jarvis.Framework.Tests.BusTests
{
    public class SampleListener : AbstractProcessManagerListener<SampleProcessManager>
    {
        public SampleListener()
        {
            Map<SampleMessage>(m => m.Id.ToString());
        }
    }
}
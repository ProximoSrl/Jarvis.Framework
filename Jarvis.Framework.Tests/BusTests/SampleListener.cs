using Jarvis.Framework.Tests.BusTests.MessageFolder;

namespace Jarvis.Framework.Tests.BusTests
{
    public class SampleListener : AbstractProcessManagerListener<SampleProcessManager, SampleProcessManagerState>
    {
        public SampleListener()
        {
            MapWithoutPrefix<SampleMessage>(m => m.Id.ToString());
        }

        public override string Prefix
        {
            get { return "SampleListener_"; }
        }
    }
}
using Jarvis.Framework.Kernel.Engine;

namespace Jarvis.Framework.Tests.BusTests
{
    public class SampleListener : AbstractProcessManagerListener<SampleProcessManager>
    {
        public SampleListener()
        {
            MapWithoutPrefix<SampleMessage>(m => m.Id.ToString());
        }

        protected override string Prefix
        {
            get { return "SampleListener_"; }
        }
    }
}
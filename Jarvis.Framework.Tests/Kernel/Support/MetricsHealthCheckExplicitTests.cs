#if NETFULL
using Jarvis.Framework.Kernel.Support;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.Kernel.Support
{
    [TestFixture]
    [Explicit]
    public class MetricsHealthCheckExplicitTests
    {
        [Test]
        public void VerifyCountOfMsmqDoesNotThrow()
        {
            //Just verify that we are able to call the api for MSMQ
            MsmqHealthCheck sut = new MsmqHealthCheck(".\\Private$\\jarvis.health", 100);
            sut.Execute();
        }
    }
}
#endif

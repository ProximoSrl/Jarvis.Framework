using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.TestHelpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.Kernel.Support
{
    [TestFixture]
    [Explicit]
    public class MetricsHealthCheckExplicitTests
    {
        [Test]
        public void VerifyCountOfMsmq()
        {
            MsmqHealthCheck sut = new MsmqHealthCheck(".\\Private$\\jarvis.health", 100, new TestLogger());
            var check = sut.Execute();
        }
    }
}

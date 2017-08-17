using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Bus.Rebus.Integration.Support;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class JarvisDetermineMessageOwnershipFromConfigurationManagerTests
    {
        private JarvisRebusConfigurationManagerRouter _sut;

        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            var map = new Dictionary<string, string>()
            {
                {"Jarvis.Framework.Tests.BusTests.MessageFolder.SampleMessage, Jarvis.Framework.Tests", "test.queue1"},
                {"Jarvis.Framework.Tests.BusTests.MessageFolder", "test.queue2"},
            };
            _sut = new JarvisRebusConfigurationManagerRouter(map);
        }

        [Test]
        public void Verify_exact_name_binding()
        {
            Assert.That(_sut.GetEndpointFor(typeof(SampleMessage)), Is.EqualTo("test.queue1"));
        }

        [Test]
        public void Verify_namespace_binding()
        {
            Assert.That(_sut.GetEndpointFor(typeof(AnotherSampleMessage)), Is.EqualTo("test.queue2"));
        }

        [Test]
        public void Verify__partial_namespace_binding()
        {
            Assert.That(_sut.GetEndpointFor(typeof(SampleMessageInFolder)), Is.EqualTo("test.queue2"));
        }
    }
}

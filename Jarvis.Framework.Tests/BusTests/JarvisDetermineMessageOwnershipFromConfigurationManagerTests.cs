using Jarvis.Framework.Rebus.Support;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class JarvisDetermineMessageOwnershipFromConfigurationManagerTests
    {
        private JarvisRebusConfiguration _sut;

        [OneTimeSetUp]
        public void TestFixtureSetup()
        {
            var map = new Dictionary<string, string>()
            {
                {"Jarvis.Framework.Tests.BusTests.MessageFolder.SampleMessage, Jarvis.Framework.Tests", "test.queueA"},
                {"Jarvis.Framework.Tests.BusTests.MessageFolder", "test.queueB"},
            };
            _sut = new JarvisRebusConfiguration("", "");
            _sut.EndpointsMap = map;
            _sut.AssembliesWithMessages = new List<Assembly>() {
                typeof(SampleMessage).Assembly
            };
        }

        [Test]
        public void Verify_exact_name_binding()
        {
            Assert.That(_sut.GetEndpointFor(typeof(SampleMessage)), Is.EqualTo("test.queuea"));
        }

        [Test]
        public void Verify_namespace_binding()
        {
            Assert.That(_sut.GetEndpointFor(typeof(AnotherSampleMessage)), Is.EqualTo("test.queueb"));
        }

        [Test]
        public void Verify__partial_namespace_binding()
        {
            Assert.That(_sut.GetEndpointFor(typeof(SampleMessageInFolder)), Is.EqualTo("test.queueb"));
        }
    }
}

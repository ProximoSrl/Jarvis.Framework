using Jarvis.Framework.Shared.Store;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.SharedTests.Store
{
    [TestFixture]
    public class VersionInfoAttributeHelperTests
    {
        [Test]
        public void Standard_event_identification()
        {
            var name = VersionInfoAttributeHelper.GetEventName(typeof(SampleAggregateCreated));
            Assert.That(name, Is.EqualTo("SampleAggregateCreated_1"));
        }

        [Test]
        public void Standard_event_identification_honor_class_name()
        {
            var name = VersionInfoAttributeHelper.GetEventName(typeof(SampleAggregateUpcasted_v1));
            Assert.That(name, Is.EqualTo("SampleAggregateUpcasted_1"));
            name = VersionInfoAttributeHelper.GetEventName(typeof(SampleAggregateUpcasted_v2));
            Assert.That(name, Is.EqualTo("SampleAggregateUpcasted_2"));
        }

        [Test]
        public void Standard_event_identification_even_if_Version_attribute_is_missing()
        {
            var name = VersionInfoAttributeHelper.GetEventName(typeof(SampleAggregateForInspection));
            Assert.That(name, Is.EqualTo("SampleAggregateForInspection_1"));
        }
    }
}

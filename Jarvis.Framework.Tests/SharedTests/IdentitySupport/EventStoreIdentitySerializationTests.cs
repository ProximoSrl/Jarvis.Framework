using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.Tests.EngineTests;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    [TestFixture]
    public class EventStoreIdentitySerializationTests
    {
        private IdentityManager _identityManager;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            _identityManager = new IdentityManager(new InMemoryCounterService());
            _identityManager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
        }

        [Test]
        public void Verify_basic_serialization()
        {
            var id = new SampleAggregateId(123);
            var serialized = JsonConvert.SerializeObject(id, new EventStoreIdentityJsonConverter(_identityManager));
            Assert.That(serialized, Is.EqualTo("\"SampleAggregate_123\""));

            //now serialize back
            var deserialzed = JsonConvert.DeserializeObject<SampleAggregateId>(serialized, new EventStoreIdentityJsonConverter(_identityManager));
            Assert.AreEqual(id, deserialzed);
        }

        [Test]
        public void Class_that_contains_id()
        {
            var id = new SampleAggregateId(123);
            var obj = new WithId() { MyId = id };
            var serialized = JsonConvert.SerializeObject(obj, new EventStoreIdentityJsonConverter(_identityManager));
            Assert.That(serialized, Is.EqualTo("{\"MyId\":\"SampleAggregate_123\"}"));

            //now serialize back
            var deserialzed = JsonConvert.DeserializeObject<WithId>(serialized, new EventStoreIdentityJsonConverter(_identityManager));
            Assert.AreEqual(obj.MyId, deserialzed.MyId);
        }

        [Test]
        public void Global_registration_of_converter()
        {
            var id = new SampleAggregateId(123);
            var obj = new WithId() { MyId = id };

            JsonConvert.DefaultSettings = () =>
                new JsonSerializerSettings()
                {
                    Converters = new List<JsonConverter> { new EventStoreIdentityJsonConverter(_identityManager) }
                };

            var serialized = JsonConvert.SerializeObject(obj);
            Assert.That(serialized, Is.EqualTo("{\"MyId\":\"SampleAggregate_123\"}"));

            //now serialize back
            var deserialzed = JsonConvert.DeserializeObject<WithId>(serialized, new EventStoreIdentityJsonConverter(_identityManager));
            Assert.AreEqual(obj.MyId, deserialzed.MyId);
        }

        [Test]
        public void Serialize_null_value()
        {
            var serialized = JsonConvert.SerializeObject(null, new EventStoreIdentityJsonConverter(_identityManager));
            Assert.That(serialized, Is.EqualTo("null"));
        }

        private class WithId
        {
            public SampleAggregateId MyId { get; set; }
        }
    }
}

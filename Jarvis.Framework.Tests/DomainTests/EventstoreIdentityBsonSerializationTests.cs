using System;
using System.Diagnostics;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.TestHelpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.DomainTests
{
    [TestFixture]
    public class DomainEventIdentityBsonSerializationTests
    {
        [BsonSerializer(typeof(EventStoreIdentityBsonSerializer))]
        public class SampleId : EventStoreIdentity
        {
            public SampleId(long id)
                : base(id)
            {
            }

            public SampleId(string id)
                : base(id)
            {
            }
        }

        public class SampleEvent : DomainEvent
        {
            public string Value { get; private set; }

            public SampleEvent(string value)
            {
                MessageId = Guid.Parse("cfbb68da-b598-417c-84c7-e951e8a36b8e");
                Value = value;
            }
        }

        [SetUp]
        public void SetUp()
        {
            var identityConverter = new IdentityManager(new InMemoryCounterService());
            EventStoreIdentityBsonSerializer.IdentityConverter = identityConverter;
            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleId).Assembly);

            BsonClassMap.RegisterClassMap<DomainEvent>(map =>
            {
                map.AutoMap();
                map.MapProperty(x => x.AggregateId).SetSerializer(new EventStoreIdentityBsonSerializer());
            });
        }

        [TearDown]
        public void TearDown()
        {
            BsonClassMapHelper.Unregister<DomainEvent>();

            // class map cleanup???
            EventStoreIdentityBsonSerializer.IdentityConverter = null;
        }

        [Test]
        public void should_serialize_event()
        {
            var e = new SampleEvent("this is a test").AssignIdForTest(new SampleId(1));
            var json = e.ToJson();
            Debug.WriteLine(json);

            Assert.AreEqual(
                "{ \"MessageId\" : CSUUID(\"cfbb68da-b598-417c-84c7-e951e8a36b8e\"), \"AggregateId\" : \"Sample_1\", \"Value\" : \"this is a test\" }",
                json
            );
        }

        [Test]
        public void should_deserialize_event()
        {
            var json = "{ \"MessageId\" : CSUUID(\"cfbb68da-b598-417c-84c7-e951e8a36b8e\"), \"AggregateId\" : \"Sample_1\", \"Value\" : \"this is a test\" }";
            var e = BsonSerializer.Deserialize<SampleEvent>(json);

            Assert.AreEqual("Sample_1", e.AggregateId.FullyQualifiedId);
        }
    }

    [TestFixture]
    public class EventstoreIdentityBsonSerializationTests 
    {
        [BsonSerializer(typeof(EventStoreIdentityBsonSerializer))]
        public class SampleId : EventStoreIdentity
        {
            public SampleId(long id)
                : base(id)
            {
            }

            public SampleId(string id)
                : base(id)
            {
            }
        }

        public class ClassWithSampleIdentity
        {
            public SampleId Value { get; set; }
        }

        [TestFixtureSetUp]
        public void SetUp()
        {
            var identityConverter = new IdentityManager(new InMemoryCounterService());
            EventStoreIdentityBsonSerializer.IdentityConverter = identityConverter;
            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleId).Assembly);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            EventStoreIdentityBsonSerializer.IdentityConverter = null;
        }

        [Test]
        public void should_serialize()
        {
            var instance = new ClassWithSampleIdentity { Value = new SampleId("Sample_1") };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : \"Sample_1\" }", json);
        }

        [Test]
        public void should_deserialize()
        {
            var instance = BsonSerializer.Deserialize<ClassWithSampleIdentity>("{ Value:\"Sample_1\"}");
            Assert.AreEqual("Sample_1", (string)instance.Value);
        }

        [Test]
        public void should_serialize_null()
        {
            var instance = new ClassWithSampleIdentity();
            var json = instance.ToJson();
            Assert.AreEqual("{ \"Value\" : null }", json);
        }

        [Test]
        public void should_deserialize_null()
        {
            var instance = BsonSerializer.Deserialize<ClassWithSampleIdentity>("{ Value: null}");
            Assert.IsNull(instance.Value);
        }

        [Test]
        public void can_convert_to_bson_value()
        {
            EventStoreIdentityCustomBsonTypeMapper.Register<SampleId>();
            var val = BsonValue.Create(new SampleId(1));
            Assert.IsInstanceOf<BsonString>(val);
        }
    }
}

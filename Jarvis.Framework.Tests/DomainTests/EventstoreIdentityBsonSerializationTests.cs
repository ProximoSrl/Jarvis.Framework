using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.TestHelpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using NUnit.Framework;
using System;
using System.Diagnostics;

namespace Jarvis.Framework.Tests.DomainTests
{
    [TestFixture]
    [Category("mongo_serialization")]
    public class DomainEventIdentityBsonSerializationTests
    {
        [BsonSerializer(typeof(TypedEventStoreIdentityBsonSerializer<SampleId>))]
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
            MongoFlatIdSerializerHelper.IdentityConverter = identityConverter;
            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleId).Assembly);

            BsonClassMap.RegisterClassMap<DomainEvent>(map =>
            {
                map.AutoMap();
                //map.MapProperty(x => x.AggregateId).SetSerializer(new TypedEventStoreIdentityBsonSerializer<EventStoreIdentity>());
            });
        }

        [TearDown]
        public void TearDown()
        {
            BsonClassMapHelper.Unregister<DomainEvent>();

            // class map cleanup???
            MongoFlatIdSerializerHelper.IdentityConverter = null;
        }

        [Test]
        public void should_serialize_event()
        {
            var e = new SampleEvent("this is a test").AssignIdForTest(new SampleId(1));
            var json = e.ToJson();
            Debug.WriteLine(json);

            Assert.AreEqual(
                "{ \"MessageId\" : \"cfbb68da-b598-417c-84c7-e951e8a36b8e\", \"AggregateId\" : \"Sample_1\", \"Value\" : \"this is a test\" }",
                json
            );
        }

        [Test]
        public void should_deserialize_event()
        {
            var json = "{ \"MessageId\" : \"cfbb68da-b598-417c-84c7-e951e8a36b8e\", \"AggregateId\" : \"Sample_1\", \"Value\" : \"this is a test\" }";
            var e = BsonSerializer.Deserialize<SampleEvent>(json);

            Assert.AreEqual(new SampleId("Sample_1"), e.AggregateId);
        }
    }
}

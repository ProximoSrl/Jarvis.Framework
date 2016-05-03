using System;
using System.Diagnostics;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.TestHelpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using NUnit.Framework;
using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.Tests.EngineTests
{
    [TestFixture]
    public class BsonIdentitySerializationTests
    {
        private SampleAggregateId _sampleAggregateId = new SampleAggregateId(1);

        const string Expected =
            "{ \"MessageId\" : \"fc3e5f0a-c4f0-47d5-91cf-a1c87fee600f\", \"AggregateId\" : \"SampleAggregate_1\" }";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            try
            {
                IdentitiesRegistration.RegisterFromAssembly(GetType().Assembly);
                var identityConverter = new IdentityManager(new InMemoryCounterService());
                EventStoreIdentityBsonSerializer.IdentityConverter = identityConverter;
                identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregate).Assembly);

                BsonClassMap.RegisterClassMap<DomainEvent>(map =>
                {
                    map.AutoMap();
                    map.MapProperty(x => x.AggregateId).SetSerializer(new EventStoreIdentityBsonSerializer());
                });
            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.ToString());
                throw;
            }
         
        }
           
        [Test]
        public void serialize_to_bson()
        {
            var evt = new SampleAggregateCreated()
            {
                MessageId = Guid.Parse("fc3e5f0a-c4f0-47d5-91cf-a1c87fee600f")
            };

            evt.AssignIdForTest(_sampleAggregateId);

            var doc = evt.ToJson();
            Debug.WriteLine(doc);

            Assert.AreEqual(Expected, doc);
        }

        [Test]
        public void deserialize_from_bson()
        {
            var d = BsonSerializer.Deserialize<SampleAggregateCreated>(Expected);
            Assert.AreEqual(Guid.Parse("fc3e5f0a-c4f0-47d5-91cf-a1c87fee600f"), d.MessageId);
        }
    }
}
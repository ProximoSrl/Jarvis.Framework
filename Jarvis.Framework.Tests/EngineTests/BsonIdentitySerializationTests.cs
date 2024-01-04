using System;
using System.Diagnostics;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.TestHelpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using NUnit.Framework;
using Jarvis.Framework.Kernel.Support;

namespace Jarvis.Framework.Tests.EngineTests
{
    [TestFixture]
    [Category("mongo_serialization")]
    public class BsonIdentitySerializationTests
    {
        private readonly SampleAggregateId _sampleAggregateId = new SampleAggregateId(1);

		private const string Expected =
            "{ \"MessageId\" : \"fc3e5f0a-c4f0-47d5-91cf-a1c87fee600f\", \"AggregateId\" : \"SampleAggregate_1\" }";

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            try
            {
                var identityConverter = new IdentityManager(new InMemoryCounterService());
                MongoFlatIdSerializerHelper.IdentityConverter = identityConverter;
                identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregate).Assembly);

                BsonClassMap.RegisterClassMap<DomainEvent>(map =>
                {
                    map.AutoMap();
                    //map.MapProperty(x => x.AggregateId).SetSerializer(new TypedEventStoreIdentityBsonSerializer<EventStoreIdentity>());
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

            NUnit.Framework.Legacy.ClassicAssert.AreEqual(Expected, doc);
        }

        [Test]
        public void deserialize_from_bson()
        {
            var d = BsonSerializer.Deserialize<SampleAggregateCreated>(Expected);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(Guid.Parse("fc3e5f0a-c4f0-47d5-91cf-a1c87fee600f"), d.MessageId);
        }
    }
}
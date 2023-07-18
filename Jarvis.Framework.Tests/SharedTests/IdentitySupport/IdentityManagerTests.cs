using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.Support;
using MongoDB.Bson;
using NUnit.Framework;
using System;
using System.Configuration;
using System.Reflection;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    [TestFixture]
    public class IdentityManagerTests
    {
        private IdentityManager sut;
        private IdentityManager sutOffline;
        private IOfflineCounterService offlineCounterService;

        [OneTimeSetUp]
        public void TestFixtureSetup()
        {
            TestHelper.RegisterSerializerForFlatId<TestId>();
            TestHelper.RegisterSerializerForFlatId<TestFlatId>();
        }

        [SetUp]
        public void SetUp()
        {
            var db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            var counterService = new CounterService(db);
            sut = new IdentityManager(counterService);
            sutOffline = new IdentityManager(offlineCounterService = new OfflineCounterService(db));
            sut.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
            sutOffline.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
            db.GetCollection<BsonDocument>("map_testid");
            db.GetCollection<BsonDocument>("map_testflatid");
            db.Drop();
        }

        [Test]
        public void verify_generate_with_reservation()
        {
            offlineCounterService.AddReservation("Test", new ReservationSlot(10, 20));
            for (Int64 i = 10; i <= 20; i++)
            {
                var next = sutOffline.New<TestId>();
                Assert.That(next, Is.EqualTo(new TestId(i)));
            }
        }

        [Test]
        public void verify_generate_plain()
        {
            for (Int64 i = 1; i <= 20; i++)
            {
                var next = sut.New<TestId>();
                Assert.That(next, Is.EqualTo(new TestId(i)));
            }
        }

        [Test]
        public async Task verify_generate_plain_async()
        {
            for (Int64 i = 1; i <= 20; i++)
            {
                var next = await sut.NewAsync<TestId>();
                Assert.That(next, Is.EqualTo(new TestId(i)));
            }
        }

        [Test]
        public async Task Verify_ability_to_get_next_generated_id()
        {
            var nextId = sut.New<TestId>();
            long peekId = await sut.PeekNextIdAsync<TestId>();
            Assert.That(peekId, Is.EqualTo(nextId.Id + 1));
        }

        [Test]
        public async Task Can_set_next_id()
        {
            var nextId = sut.New<TestId>();
            long nextForcedId = nextId.Id + 42;
            await sut.ForceNextIdAsync<TestId>(nextForcedId);

            long peekedId = await sut.PeekNextIdAsync<TestId>();
            Assert.That(peekedId, Is.EqualTo(nextForcedId));
            nextId = sut.New<TestId>();
            Assert.That(nextId.Id, Is.EqualTo(nextForcedId));
        }

        [Test]
        public void Set_next_id_in_the_past_will_throw()
        {
            var nextId = sut.New<TestId>();

            Assert.ThrowsAsync<JarvisFrameworkEngineException>(
                async () => await sut.ForceNextIdAsync<TestId>(nextId.Id));
        }
    }
}

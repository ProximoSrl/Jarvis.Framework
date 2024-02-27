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

        [TestCase("Test_1", true, 1)]
        [TestCase("Test_2", true, 2)]
        [TestCase("Test_9223372036854775808", false, 2)]
        [TestCase("Test_9223372036854775807", true, 9223372036854775807L)]
        [TestCase("Test_banana", false, 0)]
        [TestCase("_2", false, 0)]
        public void Verify_to_identity(string id, bool expected, long expectedId)
        {
            IIdentity result = null;
            try
            {
                result = sut.ToIdentity(id);
                if (!expected)
                {
                    Assert.Fail("Expected exception for invalid identity");
                }
            }
            catch (Exception)
            {
                if (expected)
                {
                    Assert.Fail("Unexpected exception for valid identity");
                }
            }

            if (expected)
            {
                Assert.That(result, Is.EqualTo(new TestId(expectedId)));
            }
        }

        [TestCase("Test_1", true, 1)]
        [TestCase("Test_2", true, 2)]
        [TestCase("Test_9223372036854775808", false, 2)]
        [TestCase("Test_9223372036854775807", true, 9223372036854775807L)]
        [TestCase("Test_banana", false, 0)]
        [TestCase("_2", false, 0)]
        public void Verify_to_identity_typed(string id, bool expected, long expectedId)
        {
            IIdentity result = null;
            try
            {
                result = sut.ToIdentity<TestId>(id);
                if (!expected)
                {
                    Assert.Fail("Expected exception for invalid identity");
                }
            }
            catch (Exception)
            {
                if (expected)
                {
                    Assert.Fail("Unexpected exception for valid identity");
                }
            }

            if (expected)
            {
                Assert.That(result, Is.EqualTo(new TestId(expectedId)));
            }
        }

        [Test]
        public void Verify_to_identity_typed_with_wrong_type()
        {
            IIdentity result = null;
            Assert.Throws<JarvisFrameworkIdentityException>(() => result = sut.ToIdentity<TestFlatId>("Test_123"));
        }

        [TestCase("Test_1", true, 1)]
        [TestCase("Test_2", true, 2)]
        [TestCase("Test_9223372036854775808", false, 2)]
        [TestCase("Test_9223372036854775807", true, 9223372036854775807L)]
        [TestCase("Test_banana", false, 0)]
        [TestCase("_2", false, 0)]
        [TestCase("", false, 0)]
        [TestCase(null, false, 0)]
        public void Verify_try_parse(string id, bool expected, long expectedId)
        {
            var result = sut.TryParse(id, out TestId typedIdentity);
            Assert.That(result, Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(typedIdentity, Is.EqualTo(new TestId(expectedId)));
            }
        }

        /// <summary>
        /// Verify that we can parse an identity using the base interface
        /// </summary>
        [Test]
        public void Verify_try_parse_with_base_interface()
        {
            var result = sut.TryParse<IIdentity>("Test_234", out var typedIdentity);
            Assert.That(result, Is.True);
            Assert.That(typedIdentity, Is.EqualTo(new TestId("Test_234")));
        }

        [Test]
        public void Verify_try_parse_with_different_id()
        {
            var result = sut.TryParse<TestFlatId>("Test_234", out var typedIdentity);
            Assert.That(result, Is.False);
            Assert.That(typedIdentity, Is.EqualTo(null));
        }

        [TestCase("Test_1", true, "Test")]
        [TestCase(null, false, "")]
        [TestCase("", false, "")]
        [TestCase("Test_2", true, "Test")]
        [TestCase("Test_a2", false, "")]
        [TestCase("Test_2a", false, "")]
        [TestCase("Test_9223372036854775808", false, "")]
        [TestCase("_1", false, "")]
        [TestCase("Test_9223372036854775807", true, "Test")]
        [TestCase("NotAnId_2", false, "")]
        [TestCase("Test_banana", false, "")]
        public void Verify_try_get_tag(string id, bool expected, string expectedTag)
        {
            var result = sut.TryGetTag(id, out string tag);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(tag, Is.EqualTo(expectedTag));
        }
    }
}

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
                Assert.That(next.AsString(), Is.EqualTo(new TestId(i).AsString()));
            }
        }

        [Test]
        public void verify_generate_plain()
        {
            for (Int64 i = 1; i <= 20; i++)
            {
                var next = sut.New<TestId>();
                Assert.That(next.AsString(), Is.EqualTo(new TestId(i).AsString()));
            }
        }

        [Test]
        public async Task verify_generate_plain_async()
        {
            for (Int64 i = 1; i <= 20; i++)
            {
                var next = await sut.NewAsync<TestId>();
                Assert.That(next.AsString(), Is.EqualTo(new TestId(i).AsString()));
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
                Assert.That(result.AsString(), Is.EqualTo(new TestId(expectedId).AsString()));
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
                Assert.That(result.AsString(), Is.EqualTo(new TestId(expectedId).AsString()));
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
                Assert.That(typedIdentity.AsString(), Is.EqualTo(new TestId(expectedId).AsString()));
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
            Assert.That(typedIdentity.AsString(), Is.EqualTo(new TestId("Test_234").AsString()));
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

        [TestCase("Test", typeof(TestId))]
        [TestCase("test", typeof(TestId))]
        [TestCase("TEST", typeof(TestId))]
        [TestCase("TestId", typeof(TestId))]
        [TestCase("testid", typeof(TestId))]
        [TestCase("TESTID", typeof(TestId))]
        [TestCase("TestFlat", typeof(TestFlatId))]
        [TestCase("testflat", typeof(TestFlatId))]
        [TestCase("TESTFLAT", typeof(TestFlatId))]
        [TestCase("TestFlatId", typeof(TestFlatId))]
        [TestCase("testflatid", typeof(TestFlatId))]
        [TestCase("TESTFLATID", typeof(TestFlatId))]
        [TestCase("NonExistentTag", null)]
        [TestCase(null, null)]
        [TestCase("", null)]
        public void Verify_get_identity_type_by_tag(string tag, Type expectedType)
        {
            var result = sut.GetIdentityTypeByTag(tag);
            Assert.That(result, Is.EqualTo(expectedType));
        }

        [Test]
        public void New_with_type_should_create_identity()
        {
            // Act
            var identity = sut.New(typeof(TestId));

            // Assert
            Assert.That(identity, Is.Not.Null);
            Assert.That(identity, Is.TypeOf<TestId>());
            Assert.That(identity.AsString(), Does.StartWith("Test_"));
        }

        [Test]
        public void New_with_null_type_should_throw_ArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => sut.New(null));
            Assert.That(ex.ParamName, Is.EqualTo("identityType"));
        }

        [Test]
        public void New_with_non_identity_type_should_throw_ArgumentException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => sut.New(typeof(string)));
            Assert.That(ex.ParamName, Is.EqualTo("identityType"));
            Assert.That(ex.Message, Does.Contain("does not implement IIdentity"));
        }

        [Test]
        public async Task NewAsync_with_type_should_create_identity()
        {
            // Act
            var identity = await sut.NewAsync(typeof(TestId));

            // Assert
            Assert.That(identity, Is.Not.Null);
            Assert.That(identity, Is.TypeOf<TestId>());
            Assert.That(identity.AsString(), Does.StartWith("Test_"));
        }

        [Test]
        public void NewAsync_with_null_type_should_throw_ArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.NewAsync((Type)null));
            Assert.That(ex.ParamName, Is.EqualTo("identityType"));
        }

        [Test]
        public void NewAsync_with_non_identity_type_should_throw_ArgumentException()
        {
            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () => await sut.NewAsync(typeof(string)));
            Assert.That(ex.ParamName, Is.EqualTo("identityType"));
            Assert.That(ex.Message, Does.Contain("does not implement IIdentity"));
        }

        [Test]
        public void Generic_New_should_use_non_generic_implementation()
        {
            // Act
            var genericResult = sut.New<TestId>();
            var nonGenericResult = sut.New(typeof(TestId));

            // Assert
            Assert.That(genericResult, Is.Not.Null);
            Assert.That(nonGenericResult, Is.Not.Null);
            Assert.That(genericResult.GetType(), Is.EqualTo(nonGenericResult.GetType()));
            Assert.That(genericResult.AsString(), Does.StartWith("Test_"));
            Assert.That(nonGenericResult.AsString(), Does.StartWith("Test_"));
        }

        [Test]
        public async Task Generic_NewAsync_should_use_non_generic_implementation()
        {
            // Act
            var genericResult = await sut.NewAsync<TestId>();
            var nonGenericResult = await sut.NewAsync(typeof(TestId));

            // Assert
            Assert.That(genericResult, Is.Not.Null);
            Assert.That(nonGenericResult, Is.Not.Null);
            Assert.That(genericResult.GetType(), Is.EqualTo(nonGenericResult.GetType()));
            Assert.That(genericResult.AsString(), Does.StartWith("Test_"));
            Assert.That(nonGenericResult.AsString(), Does.StartWith("Test_"));
        }

        [Test]
        public void New_with_type_should_generate_sequential_ids()
        {
            // Act
            var first = sut.New(typeof(TestId));
            var second = sut.New(typeof(TestId));

            // Assert
            Assert.That(first, Is.TypeOf<TestId>());
            Assert.That(second, Is.TypeOf<TestId>());

            var firstId = ((TestId)first).Id;
            var secondId = ((TestId)second).Id;
            Assert.That(secondId, Is.EqualTo(firstId + 1));
        }

        [Test]
        public async Task NewAsync_with_type_should_generate_sequential_ids()
        {
            // Act
            var first = await sut.NewAsync(typeof(TestId));
            var second = await sut.NewAsync(typeof(TestId));

            // Assert
            Assert.That(first, Is.TypeOf<TestId>());
            Assert.That(second, Is.TypeOf<TestId>());

            var firstId = ((TestId)first).Id;
            var secondId = ((TestId)second).Id;
            Assert.That(secondId, Is.EqualTo(firstId + 1));
        }

        [Test]
        public void New_with_type_should_work_with_different_identity_types()
        {
            // Act
            var testId = sut.New(typeof(TestId));
            var testFlatId = sut.New(typeof(TestFlatId));

            // Assert
            Assert.That(testId, Is.TypeOf<TestId>());
            Assert.That(testFlatId, Is.TypeOf<TestFlatId>());
            Assert.That(testId.AsString(), Does.StartWith("Test_"));
            Assert.That(testFlatId.AsString(), Does.StartWith("TestFlat_"));
        }

        [Test]
        public async Task NewAsync_with_type_should_work_with_different_identity_types()
        {
            // Act
            var testId = await sut.NewAsync(typeof(TestId));
            var testFlatId = await sut.NewAsync(typeof(TestFlatId));

            // Assert
            Assert.That(testId, Is.TypeOf<TestId>());
            Assert.That(testFlatId, Is.TypeOf<TestFlatId>());
            Assert.That(testId.AsString(), Does.StartWith("Test_"));
            Assert.That(testFlatId.AsString(), Does.StartWith("TestFlat_"));
        }
    }
}

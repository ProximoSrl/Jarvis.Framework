using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    [TestFixture]
    public class IdentityManagerTests
    {

        private IMongoCollection<BsonDocument> _mappingCollection;
        private IMongoCollection<BsonDocument> _mappingFlatCollection;
        private IdentityManager sut;
        private IdentityManager sutOffline;
        private ICounterService counterService;
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
            sut = new IdentityManager(counterService = new CounterService(db));
            sutOffline = new IdentityManager(offlineCounterService = new OfflineCounterService(db));
            sut.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
            sutOffline.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
            _mappingCollection = db.GetCollection<BsonDocument>("map_testid");
            _mappingFlatCollection = db.GetCollection<BsonDocument>("map_testflatid");
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
    }
}

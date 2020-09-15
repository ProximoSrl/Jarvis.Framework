namespace Jarvis.Framework.Tests.ServicesTests
{
    [TestFixture]
    public class OfflineCounterServiceTests
    {
        private OfflineCounterService _service;
        private IMongoDatabase _db;

        [SetUp]
        public void SetUp()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _db.Drop();
            _service = new OfflineCounterService(_db);
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            _db.Drop();
        }

        [Test]
        public void without_reservation_throws()
        {
            Assert.Throws<JarvisFrameworkEngineException>(() => _service.GetNext("test"), "without reservation it should throw");
        }

        [Test]
        public void basic_reservation()
        {
            _service.AddReservation("test", new ReservationSlot(10, 20));
            for (Int64 i = 10; i <= 20; i++)
            {
                var next = _service.GetNext("test");
                Assert.That(next, Is.EqualTo(i));
            }
        }

        [Test]
        public void retrieve_reservation_count()
        {
            _service.AddReservation("test", new ReservationSlot(10, 19));
            Int64 available = _service.CountReservation("test");
            Assert.That(available, Is.EqualTo(10));
            _service.GetNext("test");
            available = _service.CountReservation("test");
            Assert.That(available, Is.EqualTo(9));
        }

        [Test]
        public void retrieve_reservation_starving()
        {
            _service.AddReservation("test1", new ReservationSlot(10, 50));
            _service.AddReservation("test2", new ReservationSlot(10, 30));
            _service.AddReservation("test3", new ReservationSlot(10, 20));
            IEnumerable<String> starvingReservation = _service.GetReservationStarving(21);
            Assert.That(starvingReservation, Is.EquivalentTo(new[] { "test3" }));
        }

        [Test]
        public void retrieve_reservation_starving_with_series()
        {
            _service.AddReservation("test1", new ReservationSlot(10, 20));
            _service.AddReservation("test2", new ReservationSlot(10, 20));
            _service.AddReservation("test3", new ReservationSlot(10, 100));

            IEnumerable<String> starvingReservation = _service.GetReservationStarving(50, "test1", "test3", "newserie");
            Assert.That(starvingReservation, Is.EquivalentTo(new[] { "test1", "newserie" }));
        }

        [Test]
        public void basic_reservation_multiple_slot_name()
        {
            _service.AddReservation("test1", new ReservationSlot(10, 20));
            _service.AddReservation("test2", new ReservationSlot(10, 20));
            for (Int64 i = 10; i <= 20; i++)
            {
                var next1 = _service.GetNext("test1");
                Assert.That(next1, Is.EqualTo(i));
                _service.GetNext("test2");
                Assert.That(next1, Is.EqualTo(i));
            }
        }

        [Test]
        public void basic_reservation_is_only_for_a_serie_name()
        {
            _service.AddReservation("test", new ReservationSlot(10, 20));
            Assert.Throws<JarvisFrameworkEngineException>(() => _service.GetNext("another-test"), "No reservation is done for another-test");
        }

        [Test]
        public void basic_reservation_throws_when_ended()
        {
            _service.AddReservation("test", new ReservationSlot(10, 20));
            for (Int64 i = 10; i <= 20; i++)
            {
                _service.GetNext("test");
            }
            Assert.Throws<JarvisFrameworkEngineException>(() => _service.GetNext("test"), "without reservation it should throw");
        }

        [Test]
        public void add_disjoined_reservation()
        {
            _service.AddReservation("test", new ReservationSlot(10, 20));
            _service.AddReservation("test", new ReservationSlot(60, 70));
            for (Int64 i = 10; i <= 20; i++)
            {
                var next = _service.GetNext("test");
                Assert.That(next, Is.EqualTo(i));
            }

            for (Int64 i = 60; i <= 70; i++)
            {
                var next = _service.GetNext("test");
                Assert.That(next, Is.EqualTo(i));
            }
        }
    }
}

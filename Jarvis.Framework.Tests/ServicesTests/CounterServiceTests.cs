using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.IdentitySupport;
using MongoDB.Driver;
using NUnit.Framework;
using Jarvis.Framework.Shared.Helpers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using MongoDB.Bson;
using Jarvis.Framework.Shared.Exceptions;

namespace Jarvis.Framework.Tests.ServicesTests
{
    [TestFixture]
    public class CounterServiceTests
    {
        private CounterService _service;
        private IMongoDatabase _db;
        private IMongoCollection<BsonDocument> _collection;

        [SetUp]
        public void SetUp()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _collection = _db.GetCollection<BsonDocument>("sysCounters");
            _service = new CounterService(_db);
            _db.Drop();
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            _collection.Drop();
        }

        [Test]
        public void Create_first()
        {
            var first = _service.GetNext("test");
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1, first);
        }

        [Test]
        public void Ensure_Minimum_value()
        {
            var serie = Guid.NewGuid().ToString();
            _service.EnsureMinimumValue(serie, 1023);
            var generated = _service.GetNext(serie);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1023, generated);
        }

        [Test]
        public void Create_second()
        {
            _service.GetNext("test");
            var second = _service.GetNext("test");
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(2, second);
        }

        /// <summary>
        /// this is somewhat empiric, but with Wired Tiger we have problems because sometimes
        /// it fails to insert the very first element for a sequence.
        /// </summary>
        [Test]
        public void Parallel_test()
        {
            for (int j = 0; j < 100; j++)
            {
                _collection.Drop();
                Parallel.ForEach(Enumerable.Range(1, 5), i => _service.GetNext("parallel"));
                _service.GetNext("parallel");
            }
        }

        [Test]
        public void Reserve_slot_return_correct_reservation()
        {
             _service.GetNext("test");
            var reservation = _service.Reserve("test", 100);
            Assert.That(reservation.StartIndex, Is.EqualTo(2));
            Assert.That(reservation.EndIndex, Is.EqualTo(101));
        }

        [Test]
        public void Reserve_slot_as_first_call()
        {
            var reservation = _service.Reserve("test", 100);
            Assert.That(reservation.StartIndex, Is.EqualTo(1));
            Assert.That(reservation.EndIndex, Is.EqualTo(100));
        }

        [Test]
        public async Task Can_peek_next_id()
        {
            var id = Guid.NewGuid().ToString();
            var reservation = await _service.PeekNextAsync(id);
            Assert.That(reservation, Is.EqualTo(1));
            Assert.That(await _service.GetNextAsync(id), Is.EqualTo(1));
            Assert.That(await _service.GetNextAsync(id), Is.EqualTo(2));
        }

        [Test]
        public async Task Can_force_next_id()
        {
            var id = Guid.NewGuid().ToString();
            await _service.ForceNextIdAsync(id, 100);
            var reservation = await _service.PeekNextAsync(id);
            Assert.That(reservation, Is.EqualTo(100));
            Assert.That(await _service.GetNextAsync(id), Is.EqualTo(100));
        }

        [Test]
        public void Cannot_Force_negative_value()
        {
            var id = Guid.NewGuid().ToString();
            //Cannot set negative value.
            Assert.ThrowsAsync<JarvisFrameworkEngineException>(async () => await _service.ForceNextIdAsync(id, -1));
        }

        [Test]
        public async Task Force_next_id_throws_if_invalid()
        {
            var id = Guid.NewGuid().ToString();

            await _service.GetNextAsync(id);
            Assert.ThrowsAsync<JarvisFrameworkEngineException>(async () => await _service.ForceNextIdAsync(id, 1));

            await _service.ForceNextIdAsync(id, 200);

            Assert.ThrowsAsync<JarvisFrameworkEngineException>(async () => await _service.ForceNextIdAsync(id, 200));
        }

        [Test]
        public void Reserve_slot_does_not_generate_duplicate()
        {
            _service.GetNext("test");
            _service.Reserve("test", 100);
            var second = _service.GetNext("test");
            Assert.That(second, Is.EqualTo(102));
        }

        /// <summary>
        /// this is somewhat empiric, but with Wired Tiger we have problems because sometimes
        /// it fails to insert the very first element for a sequence.
        /// </summary>
        [Test]
        public void Parallel_reservation_test()
        {
            for (int j = 0; j < 10; j++)
            {
                ConcurrentBag<ReservationSlot> reservations = new ConcurrentBag<ReservationSlot>();
                _collection.Drop();
                _service.Reserve("parallel", 50); //first reservation is not multithread
                Parallel.ForEach(Enumerable.Range(1, 10), i => reservations.Add(_service.Reserve("parallel", 50)));

                var last = _service.GetNext("parallel");
                Assert.That(last, Is.EqualTo(50 * 11 + 1));
                HashSet<Int64> check = new HashSet<long>();
                foreach (var reservation in reservations)
                {
                    for (Int64 i = reservation.StartIndex; i <= reservation.EndIndex; i++)
                    {
                        Assert.That(check.Contains(i), Is.False, "Error: id " + i + " belongs to more than one reservation");
                        check.Add(i);
                    }
                }
            }
        }
    }
}

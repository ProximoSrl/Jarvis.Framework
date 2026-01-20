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
            var peeked = await _service.PeekNextAsync(id);
            Assert.That(peeked, Is.EqualTo(1));
            Assert.That(await _service.GetNextAsync(id), Is.EqualTo(1));
            Assert.That(await _service.GetNextAsync(id), Is.EqualTo(2));
            // Peek always queries database, so it returns correct value
            Assert.That(await _service.PeekNextAsync(id), Is.EqualTo(3));
        }

        [Test]
        public async Task Peek_next_id_works_after_service_restart()
        {
            var id = Guid.NewGuid().ToString();

            // Reserve some IDs with the first service instance
            _service.Reserve(id, 50);

            // Verify peek returns correct value
            var peekedBeforeRestart = await _service.PeekNextAsync(id);
            Assert.That(peekedBeforeRestart, Is.EqualTo(51));

            // Simulate service restart by creating a new CounterService instance
            var newService = new CounterService(_db);

            // Peek should still return the correct value by querying MongoDB
            var peekedAfterRestart = await newService.PeekNextAsync(id);
            Assert.That(peekedAfterRestart, Is.EqualTo(51));

            // Verify GetNext on new service returns correct value
            Assert.That(await newService.GetNextAsync(id), Is.EqualTo(51));

            // Peek should return the updated value
            Assert.That(await newService.PeekNextAsync(id), Is.EqualTo(52));
        }

        [Test]
        public async Task CheckValidity_returns_false_for_non_existent_serie()
        {
            var id = Guid.NewGuid().ToString();

            // No values have been generated, so any value should be invalid
            Assert.That(await _service.CheckValidityAsync(id, 1), Is.False);
            Assert.That(await _service.CheckValidityAsync(id, 100), Is.False);
        }

        [Test]
        public async Task CheckValidity_returns_true_for_generated_values()
        {
            var id = Guid.NewGuid().ToString();

            // Generate some IDs
            await _service.GetNextAsync(id); // 1
            await _service.GetNextAsync(id); // 2
            await _service.GetNextAsync(id); // 3

            // All generated values should be valid
            Assert.That(await _service.CheckValidityAsync(id, 1), Is.True);
            Assert.That(await _service.CheckValidityAsync(id, 2), Is.True);
            Assert.That(await _service.CheckValidityAsync(id, 3), Is.True);

            // Value not yet generated should be invalid
            Assert.That(await _service.CheckValidityAsync(id, 4), Is.False);
            Assert.That(await _service.CheckValidityAsync(id, 100), Is.False);
        }

        [Test]
        public async Task CheckValidity_uses_cache_for_known_valid_values()
        {
            var id = Guid.NewGuid().ToString();

            // Generate some IDs
            await _service.GetNextAsync(id); // 1
            await _service.GetNextAsync(id); // 2
            await _service.GetNextAsync(id); // 3

            // First call to CheckValidity should populate/use cache
            Assert.That(await _service.CheckValidityAsync(id, 2), Is.True);

            // Subsequent calls for values <= cached should use cache (fast path)
            Assert.That(await _service.CheckValidityAsync(id, 1), Is.True);
            Assert.That(await _service.CheckValidityAsync(id, 3), Is.True);
        }

        [Test]
        public async Task CheckValidity_queries_database_when_value_exceeds_cache()
        {
            var id = Guid.NewGuid().ToString();

            // Generate initial IDs
            await _service.GetNextAsync(id); // 1
            await _service.GetNextAsync(id); // 2

            // Check validity to potentially populate cache
            Assert.That(await _service.CheckValidityAsync(id, 2), Is.True);

            // Create second service that will generate more IDs
            var service2 = new CounterService(_db);
            await service2.GetNextAsync(id); // 3

            // Original service's cache might be stale, but CheckValidity
            // should query DB when value > cached and return correct result
            Assert.That(await _service.CheckValidityAsync(id, 3), Is.True);
        }

        [Test]
        public async Task CheckValidity_works_after_service_restart()
        {
            var id = Guid.NewGuid().ToString();

            // Generate IDs with first service
            await _service.GetNextAsync(id); // 1
            await _service.GetNextAsync(id); // 2
            await _service.GetNextAsync(id); // 3

            // Create new service (simulating restart, empty cache)
            var newService = new CounterService(_db);

            // CheckValidity should work by querying database
            Assert.That(await newService.CheckValidityAsync(id, 1), Is.True);
            Assert.That(await newService.CheckValidityAsync(id, 3), Is.True);
            Assert.That(await newService.CheckValidityAsync(id, 4), Is.False);

            // After first check, cache should be populated for fast subsequent checks
            Assert.That(await newService.CheckValidityAsync(id, 2), Is.True);
        }

        [Test]
        public async Task CheckValidity_works_with_reserved_slots()
        {
            var id = Guid.NewGuid().ToString();

            // Reserve a slot
            _service.Reserve(id, 50);

            // All values in the reserved range should be valid
            Assert.That(await _service.CheckValidityAsync(id, 1), Is.True);
            Assert.That(await _service.CheckValidityAsync(id, 25), Is.True);
            Assert.That(await _service.CheckValidityAsync(id, 50), Is.True);

            // Values beyond the reserved range should be invalid
            Assert.That(await _service.CheckValidityAsync(id, 51), Is.False);
        }

        [Test]
        public async Task PeekNextAsync_and_CheckValidityAsync_work_with_two_processes()
        {
            // This test simulates two different processes (service instances) sharing the same MongoDB
            var id = Guid.NewGuid().ToString();

            // Create two independent CounterService instances (simulating two processes)
            var service1 = new CounterService(_db);
            var service2 = new CounterService(_db);

            // Service 1 generates some IDs
            await service1.GetNextAsync(id); // 1
            await service1.GetNextAsync(id); // 2

            // Service 2 should see the correct next ID via PeekNextAsync (no cache, queries DB)
            Assert.That(await service2.PeekNextAsync(id), Is.EqualTo(3));

            // Service 2's CheckValidityAsync should correctly validate IDs generated by Service 1
            Assert.That(await service2.CheckValidityAsync(id, 1), Is.True);
            Assert.That(await service2.CheckValidityAsync(id, 2), Is.True);
            Assert.That(await service2.CheckValidityAsync(id, 3), Is.False);

            // Service 2 generates more IDs
            await service2.GetNextAsync(id); // 3
            await service2.GetNextAsync(id); // 4

            // Service 1's PeekNextAsync should see the updated value (queries DB, no stale cache)
            Assert.That(await service1.PeekNextAsync(id), Is.EqualTo(5));

            // Service 1's CheckValidityAsync should validate all IDs including those from Service 2
            Assert.That(await service1.CheckValidityAsync(id, 3), Is.True);
            Assert.That(await service1.CheckValidityAsync(id, 4), Is.True);
            Assert.That(await service1.CheckValidityAsync(id, 5), Is.False);
        }

        [Test]
        public async Task CheckValidityAsync_cache_does_not_cause_false_negatives_with_multiple_processes()
        {
            // This test verifies that stale cache doesn't cause false negatives
            var id = Guid.NewGuid().ToString();

            // Create two independent CounterService instances
            var service1 = new CounterService(_db);
            var service2 = new CounterService(_db);

            // Service 1 generates IDs and its CheckValidityAsync populates its cache
            await service1.GetNextAsync(id); // 1
            await service1.GetNextAsync(id); // 2
            Assert.That(await service1.CheckValidityAsync(id, 2), Is.True); // Cache now has Last=2

            // Service 2 generates more IDs (Service 1's cache is now stale)
            await service2.GetNextAsync(id); // 3
            await service2.GetNextAsync(id); // 4
            await service2.GetNextAsync(id); // 5

            // Service 1's CheckValidityAsync should still return correct results
            // Even though its cache has Last=2, when checking value=5 (which exceeds cache),
            // it should query the database and return true
            Assert.That(await service1.CheckValidityAsync(id, 5), Is.True);

            // And its cache should now be updated to reflect the DB value
            Assert.That(await service1.CheckValidityAsync(id, 4), Is.True); // Should use cache now
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
            // Cannot force to a value less than current (current is 1, trying to force to 1 means Last=0 which is < 1)
            Assert.ThrowsAsync<JarvisFrameworkEngineException>(async () => await _service.ForceNextIdAsync(id, 1));

            await _service.ForceNextIdAsync(id, 200);

            // Forcing to the same value is idempotent with $max - doesn't throw
            await _service.ForceNextIdAsync(id, 200);

            // But forcing to a lower value should throw
            Assert.ThrowsAsync<JarvisFrameworkEngineException>(async () => await _service.ForceNextIdAsync(id, 100));
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

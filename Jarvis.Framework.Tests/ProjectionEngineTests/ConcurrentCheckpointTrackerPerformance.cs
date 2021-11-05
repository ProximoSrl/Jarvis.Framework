//using Jarvis.Framework.Kernel.ProjectionEngine.Client;
//using MongoDB.Driver;
//using NUnit.Framework;
//using System;
//using System.Collections.Generic;
//using System.Configuration;
//using System.Diagnostics;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Jarvis.Framework.Tests.ProjectionEngineTests
//{
//    [TestFixture]
//    [Explicit]
//    public class ConcurrentCheckpointTrackerPerformance
//    {
//        private MongoClient _client;
//        private IMongoDatabase _db;
//        private IMongoCollection<Checkpoint> _checkpoints;
//        private const int _count = 600;
//        private const int _slotNumber = 60;

//        [SetUp]
//        public void Setup()
//        {
//            var connectionString = ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString;
//            var url = new MongoUrl(connectionString);
//            _client = new MongoClient(url);
//            _db = _client.GetDatabase(url.DatabaseName);
//            _client.DropDatabase(_db.DatabaseNamespace.DatabaseName);
//            _checkpoints = _db.GetCollection<Checkpoint>("checkpoints");
//            _checkpoints.Indexes.CreateOne(
//                    new CreateIndexModel<Checkpoint>(
//                        Builders<Checkpoint>.IndexKeys.Ascending(x => x.Slot)
//                    )
//                );
//        }

//        [Test]
//        public async Task Verify_update_performance()
//        {
//            HashSet<String> _slots = new HashSet<String>();
//            for (int i = 0; i < _count; i++)
//            {
//                var slot = $"slot{i % _slotNumber}";
//                _slots.Add(slot);
//                _checkpoints.InsertOne(new Checkpoint($"projection_{i}", 0, "1") 
//                {
//                    Slot = slot,
//                    Active = true,
//                    Value = 0,
//                    Current = 0,
//                });
//            }

//            Stopwatch stopwatch = Stopwatch.StartNew();

//            List<Task> _tasks = new List<Task>();
//            foreach (var slot in _slots)
//            {
//                var task = Task.Run(async () => await PerformCycleOfUpdate(slot).ConfigureAwait(false));
//                _tasks.Add(task);
//            }

//            Task.WaitAll(_tasks.ToArray());

//            Console.WriteLine($"Updated {_slots.Count} slots - elapsed {stopwatch.ElapsedMilliseconds}");
//        }

//        public async Task PerformCycleOfUpdate(String slot) 
//        {
//            for (int i = 0; i < 1000; i++)
//            {
//                await _checkpoints.UpdateManyAsync(
//                    Builders<Checkpoint>.Filter.Eq("Slot", slot),
//                    Builders<Checkpoint>.Update
//                        .Set(_ => _.Current, i)
//                        .Set(_ => _.Value, i)).ConfigureAwait(false);
//            }
//        }

//        [Test]
//        public void Verify_update_performance_sync()
//        {
//            HashSet<String> _slots = new HashSet<String>();
//            for (int i = 0; i < _count; i++)
//            {
//                var slot = $"slot{i % _slotNumber}";
//                _slots.Add(slot);
//                _checkpoints.InsertOne(new Checkpoint($"projection_{i}", 0, "1")
//                {
//                    Slot = slot,
//                    Active = true,
//                    Value = 0,
//                    Current = 0,
//                });
//            }

//            Stopwatch stopwatch = Stopwatch.StartNew();

//            List<Task> _tasks = new List<Task>();
//            foreach (var slot in _slots)
//            {
//                var task = Task.Run(() => PerformCycleOfUpdateSync(slot));
//                _tasks.Add(task);
//            }

//            Task.WaitAll(_tasks.ToArray());

//            Console.WriteLine($"Updated {_slots.Count} slots - elapsed {stopwatch.ElapsedMilliseconds}");
//        }

//        public void PerformCycleOfUpdateSync(String slot)
//        {
//            for (int i = 0; i < 1000; i++)
//            {
//                _checkpoints.UpdateMany(
//                    Builders<Checkpoint>.Filter.Eq("Slot", slot),
//                    Builders<Checkpoint>.Update
//                        .Set(_ => _.Current, i)
//                        .Set(_ => _.Value, i));
//            }
//        }
//    }
//}

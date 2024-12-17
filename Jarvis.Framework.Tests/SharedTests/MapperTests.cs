using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.SharedTests
{
    [TestFixture]
    [Category("mongo_serialization")]
    public class MapperTests
    {
        public class MapperTestsId : EventStoreIdentity
        {
            public MapperTestsId(long id) : base(id)
            {
            }

            [JsonConstructor]
            public MapperTestsId(string id)
                : base(id)
            {
            }
        }

        public class MapperTestMapper : AbstractIdentityTranslator<MapperTestsId>
        {
            public MapperTestMapper(IMongoDatabase systemDB, IIdentityGenerator identityGenerator)
                : base(systemDB, identityGenerator)
            {
            }

            public MapperTestsId Map(string externalKey)
            {
                return base.Translate(externalKey, true);
            }

            public void Delete(string externalKey)
            {
                var id = Translate(externalKey, false);
                base.DeleteAliases(id);
            }

            public void Replace(MapperTestsId id, String value)
            {
                base.ReplaceAlias(id, value);
            }

            public new MapperTestsId TryTranslate(string externalKey)
            {
                return base.TryTranslate(externalKey);
            }

            public new String GetAlias(MapperTestsId mapId)
            {
                return base.GetAlias(mapId);
            }
        }

        private MapperTestMapper sut;
        private IMongoCollection<BsonDocument> mapperCollection;
        private IMongoCollection<BsonDocument> counterCollection;
        private IMongoDatabase _db;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            MongoUrlBuilder mb = new MongoUrlBuilder(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            mb.DatabaseName = "jarvisframework-" + Guid.NewGuid().ToString().ToLower().Substring(0, 10);
            _db = TestHelper.CreateNew(mb.ToMongoUrl().ToString());

            IdentityManager manager = new IdentityManager(new CounterService(_db));
            manager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
            mapperCollection = _db.GetCollection<BsonDocument>("map_mappertestsid");
            counterCollection = _db.GetCollection<BsonDocument>("sysCounters");
            sut = new MapperTestMapper(_db, manager);
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            _db.Drop();
        }

        [SetUp]
        public void SetUp()
        {
            mapperCollection.Database.DropCollection(mapperCollection.CollectionNamespace.CollectionName);
            counterCollection.Database.DropCollection(counterCollection.CollectionNamespace.CollectionName);
        }

        [Test]
        public void verify_basic_mapping()
        {
            var mapId = sut.Map("TEST");
            Assert.That(mapId.AsString(), Is.EqualTo("MapperTests_1"));
        }

        [Test]
        public void verify_basic_delete()
        {
            var mapId = sut.Map("TEST");
            Assert.That(mapId.AsString(), Is.EqualTo("MapperTests_1"));
            sut.Delete("TEST");
            mapId = sut.TryTranslate("TEST");
            Assert.That(mapId, Is.Null);
        }

        [Test]
        public void verify_delete_alias_from_base_class()
        {
            var mapId = sut.Map("TEST");
            Assert.That(mapId.AsString(), Is.EqualTo("MapperTests_1"));
            sut.DeleteAliases(mapId);
            mapId = sut.TryTranslate("TEST");
            Assert.That(mapId, Is.Null);
        }

        [Test]
        public void verify_tryTranslate_with_unexisting_mapping()
        {
            var mapId = sut.TryTranslate("TEST");
            Assert.That(mapId, Is.Null);
        }

        [Test]
        public void verify_tryTranslate_with_existing_mapping()
        {
            sut.Map("TEST");
            var mapId = sut.TryTranslate("TEST");
            Assert.That(mapId.AsString(), Is.EqualTo("MapperTests_1"));
        }

        [Test]
        public void verify_change_mapping()
        {
            var mapId = sut.Map("TEST");
            Assert.That(mapId.AsString(), Is.EqualTo("MapperTests_1"));
            sut.Replace(mapId, "TEST2");
            var remapped = sut.Map("TEST2");
            Assert.That(remapped.AsString(), Is.EqualTo(mapId.AsString()));

            var mapAgain = sut.Map("TEST");
            Assert.That(mapAgain.AsString(), Is.Not.EqualTo(remapped.AsString()));
        }

        [Test]
        public void verify_reverse_translation()
        {
            var mapId = sut.Map("TEST");
            Assert.That(sut.GetAlias(mapId), Is.EqualTo("TEST".ToLower()));
        }

        [Test]
        public void verify_reverse_translation_not_existing()
        {
            Assert.That(sut.GetAlias(new MapperTestsId(-42)), Is.EqualTo(null));
        }

        [Test]
        public void verify_multithread_mapping()
        {
            for (int outerIteration = 0; outerIteration < 10; outerIteration++)
            {
                SetUp();
                Int32 iteration = 0;
                var sequence = Enumerable.Range(0, 100);
                ConcurrentBag<String> generated = new ConcurrentBag<string>();
                try
                {
                    Parallel.ForEach(sequence, i =>
                    {
                        Interlocked.Increment(ref iteration);
                        generated.Add(sut.Map("TEST" + i));
                    });
                    Assert.That(generated.Count, Is.EqualTo(100), "Error in iteration " + outerIteration);
                }
                catch (Exception ex)
                {
                    Assert.Fail("Exception at iteration " + iteration + ": " + ex.ToString());
                }

                var allRecords = mapperCollection.Find(Builders<BsonDocument>.Filter.Empty).ToList();
                Assert.That(allRecords, Has.Count.EqualTo(100));
                for (int i = 1; i <= 100; i++)
                {
                    if (!generated.Contains("MapperTests_" + i))
                        Assert.Fail("Id " + i + " is missing");
                }
                Assert.That(generated.Distinct().Count(), Is.EqualTo(100));
            }
        }

        [Test]
        public void verify_multithread_mapping_same_id()
        {
            var sequence = new Int32[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 };
            Parallel.ForEach(sequence, i =>
            {
                sut.Map("TEST" + i);
            });

            var allRecords = mapperCollection.Find(Builders<BsonDocument>.Filter.Empty).ToList();
            Assert.That(allRecords, Has.Count.EqualTo(3));
        }
    }
}

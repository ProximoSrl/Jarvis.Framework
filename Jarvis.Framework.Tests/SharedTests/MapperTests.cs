using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.SharedTests
{
    [TestFixture]
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
            public MapperTestMapper(IMongoDatabase systemDB, IIdentityGenerator identityGenerator) : base(systemDB, identityGenerator)
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
        }

        private MapperTestMapper sut;
        private IMongoCollection<BsonDocument> mapperCollection;
        private IMongoCollection<BsonDocument> counterCollection;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            IdentityManager manager = new IdentityManager(new CounterService(db));
            manager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
            mapperCollection = db.GetCollection<BsonDocument>("map_mappertestsid");
            counterCollection = db.GetCollection<BsonDocument>("sysCounters");
            sut = new MapperTestMapper(db, manager);
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
            var mapId = sut.Map("TEST");
            mapId = sut.TryTranslate("TEST");
            Assert.That(mapId.AsString(), Is.EqualTo("MapperTests_1"));
        }

        [Test]
        public void verify_change_mapping()
        {
            var mapId = sut.Map("TEST");
            Assert.That(mapId.AsString(), Is.EqualTo("MapperTests_1"));
            sut.Replace(mapId, "TEST2");
            var reMapped = sut.Map("TEST2");
            Assert.That(mapId.AsString(), Is.EqualTo("MapperTests_1"));
        }

        [Test]
        public void verify_multithread_mapping()
        {
            var sequence = Enumerable.Range(1, 100);
            Parallel.ForEach(sequence, i =>
            {
                sut.Map("TEST" + i);
            });

            var allRecords = mapperCollection.Find(Builders<BsonDocument>.Filter.Empty).ToList();
            Assert.That(allRecords, Has.Count.EqualTo(100));
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

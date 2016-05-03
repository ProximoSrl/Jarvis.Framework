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

            public MapperTestsId Map(string containerName)
            {
                return base.Translate(containerName, true);
            }
        }

        private MapperTestMapper sut;
        private IMongoCollection<BsonDocument> _collection;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            IdentityManager manager = new IdentityManager(new CounterService(db));
            manager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
            _collection = db.GetCollection<BsonDocument>("map_mappertestsid");
            sut = new MapperTestMapper(db, manager);
        }

        [SetUp]
        public void SetUp()
        {
            _collection.Database.DropCollection(_collection.CollectionNamespace.CollectionName);
        }

        [Test]
        public void verify_basic_mapping()
        {
            var mapId = sut.Map("TEST");
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

            var allRecords = _collection.Find(Builders<BsonDocument>.Filter.Empty).ToList();
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

            var allRecords = _collection.Find(Builders<BsonDocument>.Filter.Empty).ToList();
            Assert.That(allRecords, Has.Count.EqualTo(3));
        }
    }
}

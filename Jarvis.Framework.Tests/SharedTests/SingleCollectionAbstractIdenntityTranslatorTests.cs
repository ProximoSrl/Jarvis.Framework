#if NET5_0_OR_GREATER //Avoid running for both framework.
using Jarvis.Framework.Shared.Exceptions;
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
    public class SingleCollectionAbstractIdenntityTranslatorTests
    {
        public class ScMapperTestsId : EventStoreIdentity
        {
            public ScMapperTestsId(long id) : base(id)
            {
            }

            [JsonConstructor]
            public ScMapperTestsId(string id)
                : base(id)
            {
            }
        }

        public class ScMapperTests2Id : EventStoreIdentity
        {
            public ScMapperTests2Id(long id) : base(id)
            {
            }

            [JsonConstructor]
            public ScMapperTests2Id(string id)
                : base(id)
            {
            }
        }

        /// <summary>
        /// Old test mapper is needed for migration
        /// </summary>
        public class OldMapper : AbstractIdentityTranslator<ScMapperTestsId>
        {
            public OldMapper(IMongoDatabase systemDB, IIdentityGenerator identityGenerator)
                : base(systemDB, identityGenerator)
            {
            }

            public ScMapperTestsId Map(string externalKey)
            {
                return base.Translate(externalKey, true);
            }

            public void Delete(string externalKey)
            {
                var id = Translate(externalKey, false);
                base.DeleteAliases(id);
            }

            public void Replace(ScMapperTestsId id, String value)
            {
                base.ReplaceAlias(id, value);
            }

            public new ScMapperTestsId TryTranslate(string externalKey)
            {
                return base.TryTranslate(externalKey);
            }

            public new String GetAlias(ScMapperTestsId mapId)
            {
                return base.GetAlias(mapId);
            }
        }

        /// <summary>
        /// Old test mapper is needed for migration
        /// </summary>
        public class OldMapper2 : AbstractIdentityTranslator<ScMapperTests2Id>
        {
            public OldMapper2(IMongoDatabase systemDB, IIdentityGenerator identityGenerator)
                : base(systemDB, identityGenerator)
            {
            }

            public ScMapperTests2Id Map(string externalKey)
            {
                return base.Translate(externalKey, true);
            }

            public void Delete(string externalKey)
            {
                var id = Translate(externalKey, false);
                base.DeleteAliases(id);
            }

            public void Replace(ScMapperTests2Id id, String value)
            {
                base.ReplaceAlias(id, value);
            }

            public new ScMapperTests2Id TryTranslate(string externalKey)
            {
                return base.TryTranslate(externalKey);
            }

            public new String GetAlias(ScMapperTests2Id mapId)
            {
                return base.GetAlias(mapId);
            }
        }

        public class MapperTestMapper : SingleCollectionAbstractIdentityTranslator<ScMapperTestsId>
        {
            public MapperTestMapper(IMongoDatabase systemDB, IIdentityGenerator identityGenerator)
                : base(systemDB, identityGenerator)
            {
            }

            public ScMapperTestsId Map(string externalKey)
            {
                return base.Translate(externalKey, true);
            }

            public void Delete(string externalKey)
            {
                var id = Translate(externalKey, false);
                base.DeleteAliases(id);
            }

            public void Replace(ScMapperTestsId id, String value)
            {
                base.ReplaceAlias(id, value);
            }

            public new ScMapperTestsId TryTranslate(string externalKey)
            {
                return base.TryTranslate(externalKey);
            }

            public new String GetAlias(ScMapperTestsId mapId)
            {
                return base.GetAlias(mapId);
            }
        }

        public class MapperTestMapper2 : SingleCollectionAbstractIdentityTranslator<ScMapperTests2Id>
        {
            public MapperTestMapper2(IMongoDatabase systemDB, IIdentityGenerator identityGenerator)
                : base(systemDB, identityGenerator)
            {
            }

            public ScMapperTests2Id Map(string externalKey)
            {
                return base.Translate(externalKey, true);
            }

            public void Delete(string externalKey)
            {
                var id = Translate(externalKey, false);
                base.DeleteAliases(id);
            }

            public void Replace(ScMapperTests2Id id, String value)
            {
                base.ReplaceAlias(id, value);
            }

            public new ScMapperTests2Id TryTranslate(string externalKey)
            {
                return base.TryTranslate(externalKey);
            }

            public new String GetAlias(ScMapperTests2Id mapId)
            {
                return base.GetAlias(mapId);
            }
        }

        private MapperTestMapper sut;
        private OldMapper _oldMapper;

        private IMongoCollection<BsonDocument> mapperCollection;
        private IMongoCollection<BsonDocument> oldMapCollection;
        private IMongoCollection<BsonDocument> counterCollection;
        private IMongoDatabase _db;
        private IdentityManager _manager;
        private IMongoCollection<BsonDocument> oldMapCollection2;
        private MapperTestMapper2 sut2;
        private OldMapper2 _oldMapper2;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            MongoUrlBuilder mb = new MongoUrlBuilder(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            mb.DatabaseName = "jarvisframework-" + Guid.NewGuid().ToString().ToLower().Substring(0, 10);
            _db = TestHelper.CreateNew(mb.ToMongoUrl().ToString());

            _manager = new IdentityManager(new CounterService(_db));
            _manager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
            mapperCollection = _db.GetCollection<BsonDocument>("mappers");
            oldMapCollection = _db.GetCollection<BsonDocument>("map_scmappertestsid");
            counterCollection = _db.GetCollection<BsonDocument>("sysCounters");
            sut = new MapperTestMapper(_db, _manager);
            _oldMapper = new OldMapper(_db, _manager);

            oldMapCollection2 = _db.GetCollection<BsonDocument>("map_scmappertests2id");

            sut2 = new MapperTestMapper2(_db, _manager);
            _oldMapper2 = new OldMapper2(_db, _manager);
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
            Assert.That(mapId.AsString(), Is.EqualTo("ScMapperTests_1"));
        }

        [Test]
        public void verify_moving_data_from_old_mapper()
        {
            _db.Drop();
            oldMapCollection.DeleteMany(Builders<BsonDocument>.Filter.Empty);
            var mapId = _oldMapper.Map("TEST_old_mapping");
            var allCount = oldMapCollection.AsQueryable().Count();
            Assert.That(allCount, Is.EqualTo(1));

            var collections = _db.ListCollectionNames().ToList();
            Assert.That(collections.Contains(oldMapCollection.CollectionNamespace.CollectionName), Is.True);

            var anotherSut = new MapperTestMapper(_db, _manager);

            //now we must have already moved the mapping from old to new
            var mapId2 = anotherSut.TryTranslate("TEST_old_mapping");
            Assert.That(mapId2.AsString(), Is.EqualTo(mapId.AsString()));

            collections = _db.ListCollectionNames().ToList();
            //collection must be deleted
            Assert.That(collections.Contains(oldMapCollection.CollectionNamespace.CollectionName), Is.False);
        }

        [Test]
        public void verify_moving_data_from_old_mapper_is_not_possible_After_first_mapping()
        {
            _db.Drop();

            var anotherSut = new MapperTestMapper(_db, _manager);
            // we insert a record in the mapper collection
            anotherSut.Map("TEST1");

            //now simulate data in the old mapper.
            var mapId = _oldMapper.Map("TEST_old_mapping");
            var allCount = oldMapCollection.AsQueryable().Count();
            Assert.That(allCount, Is.EqualTo(1));

            //We expect that creating another mapper, it will detect data on new mapping, data on
            //the old mapping and thus it will throw an exception.
            Assert.Throws<JarvisFrameworkEngineException>(() => new MapperTestMapper(_db, _manager));   
        }

        [Test]
        public void verify_moving_data_from_old_mapper_only_the_first_time()
        {
            _db.Drop();
            oldMapCollection.DeleteMany(Builders<BsonDocument>.Filter.Empty);
            var mapId = _oldMapper.Map("TEST_old_mapping");
            var allCount = oldMapCollection.AsQueryable().Count();
            Assert.That(allCount, Is.EqualTo(1));

            var collections = _db.ListCollectionNames().ToList();
            Assert.That(collections.Contains(oldMapCollection.CollectionNamespace.CollectionName), Is.True);

            var anotherSut = new MapperTestMapper(_db, _manager);
            Assert.That(this.mapperCollection.AsQueryable().Count(), Is.EqualTo(1));
            Assert.That(this.oldMapCollection.AsQueryable().Count(), Is.EqualTo(0));

            //now we must have already moved the mapping from old to new
            var mapId2 = anotherSut.TryTranslate("TEST_old_mapping");
            Assert.That(mapId2.AsString(), Is.EqualTo(mapId.AsString()));

            collections = _db.ListCollectionNames().ToList();
            //collection must be deleted
            Assert.That(collections.Contains(oldMapCollection.CollectionNamespace.CollectionName), Is.False);

            //this must not throw or alter results
            new MapperTestMapper(_db, _manager);
            Assert.That(this.mapperCollection.AsQueryable().Count(), Is.EqualTo(1));
            Assert.That(this.oldMapCollection.AsQueryable().Count(), Is.EqualTo(0));
        }

        [Test]
        public void verify_moving_data_from_old_mapper_multiple_mappers()
        {
            _db.Drop();
            var mapId = _oldMapper.Map("TEST_old_mapping");
            var mapId2 = _oldMapper2.Map("TEST_old_mapping");

            var allCount = oldMapCollection.AsQueryable().Count();
            Assert.That(allCount, Is.EqualTo(1));

            var allCount2 = oldMapCollection2.AsQueryable().Count();
            Assert.That(allCount2, Is.EqualTo(1));

            Assert.That(mapId.Id, Is.EqualTo(mapId2.Id));   

            var collections = _db.ListCollectionNames().ToList();
            Assert.That(collections.Contains(oldMapCollection.CollectionNamespace.CollectionName), Is.True);
            Assert.That(collections.Contains(oldMapCollection2.CollectionNamespace.CollectionName), Is.True);

            var anotherSut = new MapperTestMapper(_db, _manager);
            //ok still the second one must exists
            collections = _db.ListCollectionNames().ToList();
            Assert.That(collections.Contains(oldMapCollection.CollectionNamespace.CollectionName), Is.False, "First mapping collection must be gone");
            Assert.That(collections.Contains(oldMapCollection2.CollectionNamespace.CollectionName), Is.True);

            Assert.That(this.mapperCollection.AsQueryable().Count(), Is.EqualTo(1));

            var mapId_on_new_collection = anotherSut.TryTranslate("TEST_old_mapping");
            Assert.That(mapId_on_new_collection.AsString(), Is.EqualTo(mapId.AsString()));

            //Assert before and after that we are not altering the collection.
            Assert.That(this.mapperCollection.AsQueryable().Count(), Is.EqualTo(1));
            var anotherSut2 = new MapperTestMapper2(_db, _manager);
            Assert.That(this.mapperCollection.AsQueryable().Count(), Is.EqualTo(2), "In the single collection we need to have now two mapping");

            mapId_on_new_collection = anotherSut.TryTranslate("TEST_old_mapping");
            Assert.That(mapId_on_new_collection.AsString(), Is.EqualTo(mapId.AsString()));

            var mapId2_on_new_collection = anotherSut2.TryTranslate("TEST_old_mapping");
            Assert.That(mapId2_on_new_collection.AsString(), Is.EqualTo(mapId2.AsString()));

            collections = _db.ListCollectionNames().ToList();
            Assert.That(collections.Contains(oldMapCollection.CollectionNamespace.CollectionName), Is.False, "First mapping collection must be gone");
            Assert.That(collections.Contains(oldMapCollection2.CollectionNamespace.CollectionName), Is.False, "Second mapping collection must be gone");
        }

        [Test]
        public void verify_basic_delete()
        {
            var mapId = sut.Map("TEST");
            Assert.That(mapId.AsString(), Is.EqualTo("ScMapperTests_1"));
            sut.Delete("TEST");
            mapId = sut.TryTranslate("TEST");
            Assert.That(mapId, Is.Null);
        }

        [Test]
        public void verify_delete_alias_from_base_class()
        {
            var mapId = sut.Map("TEST");
            Assert.That(mapId.AsString(), Is.EqualTo("ScMapperTests_1"));
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
            Assert.That(mapId.AsString(), Is.EqualTo("ScMapperTests_1"));
        }

        [Test]
        public void verify_change_mapping()
        {
            var mapId = sut.Map("TEST");
            Assert.That(mapId.AsString(), Is.EqualTo("ScMapperTests_1"));
            sut.Replace(mapId, "TEST2");
            sut.Map("TEST2");
            Assert.That(mapId.AsString(), Is.EqualTo("ScMapperTests_1"));
        }

        [Test]
        public void verify_reverse_translation()
        {
            var mapId = sut.Map("TEST");
            Assert.That(sut.GetAlias(mapId), Is.EqualTo("scmappertestsid_TEST".ToLower()));
        }

        [Test]
        public void verify_reverse_translation_not_existing()
        {
            Assert.That(sut.GetAlias(new ScMapperTestsId(-42)), Is.EqualTo(null));
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
                    if (!generated.Contains("ScMapperTests_" + i))
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
#endif
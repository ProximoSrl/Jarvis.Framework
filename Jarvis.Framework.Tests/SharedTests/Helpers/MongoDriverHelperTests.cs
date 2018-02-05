using MongoDB.Bson;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.SharedTests.Helpers
{
    [TestFixture]
    public class MongoDriverHelperTests
    {
        public class MongoDriverHelperTestsClass
        {
            public String Id { get; set; }

            public String Name { get; set; }
        }

        public class MongoDriverHelperTestsClassWithObjectId
        {
            public ObjectId Id { get; set; }

            public String Name { get; set; }
        }

        private IMongoCollection<MongoDriverHelperTestsClass> _collection;
        private IMongoCollection<MongoDriverHelperTestsClassWithObjectId> _collectionObjectId;

        [SetUp]
        public void SetUp()
        {
            var conn = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(conn);
            var db = client.GetDatabase(conn.DatabaseName);
            _collection = db.GetCollection<MongoDriverHelperTestsClass>("mongo-helpers-test");
            _collectionObjectId = db.GetCollection<MongoDriverHelperTestsClassWithObjectId>("mongo-helpers-test-objectId");
            _collection.Drop();
            _collectionObjectId.Drop();
        }

        [Test]
        public void Verify_find_one_by_id()
        {
            _collection.InsertMany(new MongoDriverHelperTestsClass[] {
                new MongoDriverHelperTestsClass() { Id ="A", Name = "A"},
                new MongoDriverHelperTestsClass() { Id ="B", Name = "B"},
            });

            var findOne = _collection.FindOneById("A");
            Assert.That(findOne, Is.Not.Null);
            Assert.That(findOne.Name, Is.EqualTo("A"));
        }

        [Test]
        public void Verify_find_one_by_id_bsonValue()
        {
            _collection.InsertMany(new MongoDriverHelperTestsClass[] {
                new MongoDriverHelperTestsClass() { Id ="A", Name = "A"},
                new MongoDriverHelperTestsClass() { Id ="B", Name = "B"},
            });

            var findOne = _collection.FindOneById(BsonValue.Create("A"));
            Assert.That(findOne, Is.Not.Null);
            Assert.That(findOne.Name, Is.EqualTo("A"));
        }

        [Test]
        public void verify_object_id_is_generated()
        {
            MongoDriverHelperTestsClassWithObjectId obj = new MongoDriverHelperTestsClassWithObjectId() { Name = "A" };
            _collectionObjectId.SaveWithGeneratedObjectId(obj, id => obj.Id = id);
            Assert.That(obj.Id, Is.Not.EqualTo(ObjectId.Empty));
            var saved = _collectionObjectId.FindOneById(obj.Id);
            Assert.That(saved.Name, Is.EqualTo("A"));
        }

        [Test]
        public void verify_save_with_empty_object_id_throws()
        {
            MongoDriverHelperTestsClassWithObjectId obj = new MongoDriverHelperTestsClassWithObjectId() { Name = "A" };
            Assert.That(() =>_collectionObjectId.Save(obj, obj.Id),
                Throws.Exception.With.Message.Contains("Cannot save with null objectId"));
        }
    }
}

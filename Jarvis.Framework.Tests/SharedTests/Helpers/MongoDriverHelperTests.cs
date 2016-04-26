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
        IMongoCollection<MongoDriverHelperTestsClass> _collection;

        [SetUp]
        public void SetUp()
        {
            var conn = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(conn);
            var db = client.GetDatabase(conn.DatabaseName);
            _collection = db.GetCollection<MongoDriverHelperTestsClass>("mongo-helpers-test");
            _collection.Drop();
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
    }
}

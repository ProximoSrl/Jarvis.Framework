using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Tests.ProjectionEngineTests;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    [TestFixture]
    class MongoStorageTests
    {
        private MongoStorage<SampleReadModel, String> _sut;
        private IMongoCollection<SampleReadModel> _collection;
        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(url);
            var readmodelDb = client.GetDatabase(url.DatabaseName);
            _collection = readmodelDb.GetCollection<SampleReadModel>("SampleReadModel");
            _sut = new MongoStorage<SampleReadModel, string>(_collection);
        }

        [SetUp]
        public void SetUp()
        {
            _collection.Drop();
        }

        [Test]
        public async Task Verify_create_index_change_fields()
        {
            await _sut.CreateIndexAsync(
                "test",
                Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp),
                new CreateIndexOptions() { Name = "TestIndex" });

            //now modify the index, should not throw
            await _sut.CreateIndexAsync(
                 "test",
                 Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp)
                    .Descending(x => x.IsInRebuild),
                 new CreateIndexOptions() { Name = "TestIndex" });

            var index = _collection.Indexes.List().ToList().Single(x => x["name"].AsString == "test");
            Assert.That(index["name"].AsString, Is.EqualTo("test"));
            Assert.That(index["key"]["Timestamp"].AsInt32, Is.EqualTo(1));
            Assert.That(index["key"]["IsInRebuild"].AsInt32, Is.EqualTo(-1));
        }

        [Test]
        public async Task Verify_update_index_with_different_options()
        {
            await _sut.CreateIndexAsync(
                "test1",
                Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp),
               new CreateIndexOptions() { Unique = false });

            //now modify the index, should not throw
            await _sut.CreateIndexAsync(
                 "test1",
                 Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp),
               new CreateIndexOptions() { Unique = true });

            var index = _collection.Indexes.List().ToList().Single(x => x["name"].AsString == "test1");

            Assert.That(index["name"].AsString, Is.EqualTo("test1"));
            Assert.That(index["key"]["Timestamp"].AsInt32, Is.EqualTo(1));
            Assert.That(index["unique"].AsBoolean, Is.True);
        }

        [Test]
        public async Task Verify_create_index_different_options_no_name_descending()
        {
            await _sut.CreateIndexAsync(
                "test2",
                Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp)
                    .Descending(x => x.IsInRebuild),
                new CreateIndexOptions() { Unique = false });

            //now modify the index, should not throw
            await _sut.CreateIndexAsync(
                 "test2",
                Builders<SampleReadModel>.IndexKeys
                        .Ascending(x => x.Timestamp)
                        .Descending(x => x.IsInRebuild),
            new CreateIndexOptions() { Unique = true });

            var index = _collection.Indexes.List().ToList().Single(x => x["name"].AsString == "test2");

            Assert.That(index["name"].AsString, Is.EqualTo("test2"));
            Assert.That(index["key"]["Timestamp"].AsInt32, Is.EqualTo(1));
            Assert.That(index["key"]["IsInRebuild"].AsInt32, Is.EqualTo(-1));
            Assert.That(index["unique"].AsBoolean, Is.True);
        }

        [Test]
        public async Task Verify_create_index_multiple_times_without_options()
        {
            await _sut.CreateIndexAsync(
                 "test1",
                  Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp));

            //now modify the index, should not throw
            await _sut.CreateIndexAsync(
                 "test2",
                  Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp)
                    .Ascending(x => x.IsInRebuild));

            var index = _collection.Indexes.List().ToList().Count;
            Assert.That(index, Is.EqualTo(3)); //original _id index plus my two indexes
        }
    }
}

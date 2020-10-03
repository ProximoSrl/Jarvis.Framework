using Castle.Core.Logging;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Tests.ProjectionEngineTests;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    [TestFixture]
    public class MongoStorageTests
    {
        private MongoStorage<SampleReadModel, String> _sut;
        private IMongoCollection<SampleReadModel> _collection;

        [OneTimeSetUp]
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
            _sut.Logger = new TestLogger(LoggerLevel.Info);
        }

        [Test]
        public async Task Verify_create_index_change_fields()
        {
            await _sut.CreateIndexAsync(
                "test",
                Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp),
                new CreateIndexOptions() { Name = "TestIndex" }).ConfigureAwait(false);

            //now modify the index, should not throw
            await _sut.CreateIndexAsync(
                 "test",
                 Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp)
                    .Descending(x => x.IsInRebuild),
                 new CreateIndexOptions() { Name = "TestIndex" }).ConfigureAwait(false);

            var index = _collection.Indexes.List().ToList().Single(x => x["name"].AsString == "test");
            Assert.That(index["name"].AsString, Is.EqualTo("test"));
            Assert.That(index["key"]["Timestamp"].AsInt32, Is.EqualTo(1));
            Assert.That(index["key"]["IsInRebuild"].AsInt32, Is.EqualTo(-1));
        }

        [Test]
        public async Task Verify_ignore_error_when_create_invalid_index()
        {
            using (TestLogger.GlobalEnableStartScope())
            {
                await _sut.CreateIndexAsync(
                "", //empty index is not valid.
                Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp),
                new CreateIndexOptions() { Name = "TestIndex" }).ConfigureAwait(false);

                //Error is ignored but error was logged.
                Assert.That(((TestLogger)_sut.Logger).ErrorCount, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task Verify_update_index_with_different_options()
        {
            await _sut.CreateIndexAsync(
                "test1",
                Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp),
               new CreateIndexOptions() { Unique = false }).ConfigureAwait(false);

            //now modify the index, should not throw
            await _sut.CreateIndexAsync(
                 "test1",
                 Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp),
               new CreateIndexOptions() { Unique = true }).ConfigureAwait(false);

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
                new CreateIndexOptions() { Unique = false }).ConfigureAwait(false);

            //now modify the index, should not throw
            await _sut.CreateIndexAsync(
                 "test2",
                Builders<SampleReadModel>.IndexKeys
                        .Ascending(x => x.Timestamp)
                        .Descending(x => x.IsInRebuild),
            new CreateIndexOptions() { Unique = true }).ConfigureAwait(false);

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
                    .Ascending(x => x.Timestamp)).ConfigureAwait(false);

            //now modify the index, should not throw
            await _sut.CreateIndexAsync(
                 "test2",
                  Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp)
                    .Ascending(x => x.IsInRebuild)).ConfigureAwait(false);

            var index = _collection.Indexes.List().ToList().Count;
            Assert.That(index, Is.EqualTo(3)); //original _id index plus my two indexes
        }
    }
}

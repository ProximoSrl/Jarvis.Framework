using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Tests.ProjectionEngineTests;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    [TestFixture]
    class MongoStorageTests
    {
        private MongoStorage<SampleReadModel, String> _sut;
        private MongoCollection<SampleReadModel> _collection;
        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(url);
            var readmodelDb = client.GetServer().GetDatabase(url.DatabaseName);
            _collection = readmodelDb.GetCollection<SampleReadModel>("SampleReadModel");
            _sut = new MongoStorage<SampleReadModel, string>(_collection);
        }

        [SetUp]
        public void SetUp()
        {
            _collection.Drop();
        }

        [Test]
        public void Verify_create_index_change_fields()
        {
            _sut.CreateIndex(
                IndexKeys<SampleReadModel>
                    .Ascending(x => x.Timestamp),
                IndexOptions.SetName("TestIndex"));

            //now modify the index, should not throw
            _sut.CreateIndex(
                IndexKeys<SampleReadModel>
                    .Ascending(x => x.Timestamp)
                    .Descending(x => x.IsInRebuild),
                IndexOptions.SetName("TestIndex"));

            var index = _collection.GetIndexes().Single(x => x.Name == "TestIndex");
            Assert.That(index.Name, Is.EqualTo("TestIndex"));
            Assert.That(index.Key["Timestamp"].AsInt32, Is.EqualTo(1));
            Assert.That(index.Key["IsInRebuild"].AsInt32, Is.EqualTo(-1));
        }

        [Test]
        public void Verify_create_index_different_options_no_name()
        {
            _sut.CreateIndex(
                IndexKeys<SampleReadModel>
                    .Ascending(x => x.Timestamp),
                IndexOptions.SetUnique(false));

            //now modify the index, should not throw
            _sut.CreateIndex(
                IndexKeys<SampleReadModel>
                    .Ascending(x => x.Timestamp),
                IndexOptions.SetUnique(true));

            var index = _collection.GetIndexes().Single(x => x.Name == "Timestamp_1");

            Assert.That(index.Name, Is.EqualTo("Timestamp_1"));
            Assert.That(index.Key["Timestamp"].AsInt32, Is.EqualTo(1));
            Assert.That(index.IsUnique, Is.True);
        }

        [Test]
        public void Verify_create_index_different_options_no_name_descending()
        {
            _sut.CreateIndex(
                IndexKeys<SampleReadModel>
                    .Ascending(x => x.Timestamp)
                    .Descending(x => x.IsInRebuild),
                IndexOptions.SetUnique(false));

            //now modify the index, should not throw
            _sut.CreateIndex(
                IndexKeys<SampleReadModel>
                    .Ascending(x => x.Timestamp)
                    .Descending(x => x.IsInRebuild),
                IndexOptions.SetUnique(true));

            var index = _collection.GetIndexes().Single(x => x.Name == "Timestamp_1_IsInRebuild_-1");

            Assert.That(index.Name, Is.EqualTo("Timestamp_1_IsInRebuild_-1"));
            Assert.That(index.Key["Timestamp"].AsInt32, Is.EqualTo(1));
            Assert.That(index.Key["IsInRebuild"].AsInt32, Is.EqualTo(-1));
            Assert.That(index.IsUnique, Is.True);
        }

        [Test]
        public void Verify_create_index_multiple_times_without_options()
        {
            _sut.CreateIndex(
                IndexKeys<SampleReadModel>
                    .Ascending(x => x.Timestamp));

            //now modify the index, should not throw
            _sut.CreateIndex(
                IndexKeys<SampleReadModel>
                    .Ascending(x => x.Timestamp)
                    .Ascending(x => x.IsInRebuild));

            var index = _collection.GetIndexes().Count;
            Assert.That(index, Is.EqualTo(3)); //original _id index plus my two indexes
        }
    }
}

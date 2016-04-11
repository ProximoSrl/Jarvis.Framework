﻿using Jarvis.Framework.Kernel.ProjectionEngine;
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
        public void Verify_create_index_change_fields()
        {
            _sut.CreateIndex(
                Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp),
                new CreateIndexOptions() { Name = "TestIndex" });

            //now modify the index, should not throw
            _sut.CreateIndex(
                 Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp)
                    .Descending(x => x.IsInRebuild),
                 new CreateIndexOptions() { Name = "TestIndex" });

            var index = _collection.Indexes.List().ToList().Single(x => x["name"].AsString == "TestIndex");
            Assert.That(index["name"].AsString, Is.EqualTo("TestIndex"));
            Assert.That(index["Timestamp"].AsInt32, Is.EqualTo(1));
            Assert.That(index["IsInRebuild"].AsInt32, Is.EqualTo(-1));
        }

        [Test]
        public void Verify_create_index_different_options_no_name()
        {
            _sut.CreateIndex(
                Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp),
               new CreateIndexOptions() { Unique = false });

            //now modify the index, should not throw
            _sut.CreateIndex(
                 Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp),
               new CreateIndexOptions() { Unique = true });

            var index = _collection.Indexes.List().ToList().Single(x => x["name"].AsString == "Timestamp_1");

            Assert.That(index["name"].AsString, Is.EqualTo("Timestamp_1"));
            Assert.That(index["Timestamp"].AsInt32, Is.EqualTo(1));
            Assert.That(index["unique"].AsBoolean, Is.True);
        }

        [Test]
        public void Verify_create_index_different_options_no_name_descending()
        {
            _sut.CreateIndex(
                Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp)
                    .Descending(x => x.IsInRebuild),
                new CreateIndexOptions() { Unique = false });

            //now modify the index, should not throw
            _sut.CreateIndex(
            Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp)
                    .Descending(x => x.IsInRebuild),
            new CreateIndexOptions() { Unique = true });

            var index = _collection.Indexes.List().ToList().Single(x => x["name"].AsString == "Timestamp_1_IsInRebuild_-1");

            Assert.That(index["name"].AsString, Is.EqualTo("Timestamp_1_IsInRebuild_-1"));
            Assert.That(index["Timestamp"].AsInt32, Is.EqualTo(1));
            Assert.That(index["IsInRebuild"].AsInt32, Is.EqualTo(-1));
            Assert.That(index["unique"].AsBoolean, Is.True);
        }

        [Test]
        public void Verify_create_index_multiple_times_without_options()
        {
            _sut.CreateIndex(
                  Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp));

            //now modify the index, should not throw
            _sut.CreateIndex(
                  Builders<SampleReadModel>.IndexKeys
                    .Ascending(x => x.Timestamp)
                    .Ascending(x => x.IsInRebuild));

            var index = _collection.Indexes.List().ToList().Count;
            Assert.That(index, Is.EqualTo(3)); //original _id index plus my two indexes
        }
    }
}

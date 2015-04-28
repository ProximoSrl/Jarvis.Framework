﻿using Jarvis.Framework.Kernel.ProjectionEngine;
using MongoDB.Bson;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    [TestFixture]
    public class CachedMongoStorageTest
    {
        private CachedMongoStorage2<MyReadModel, String> sut;
        private MongoDatabase db;
        private MongoCollection<MyReadModel> collection;

        [TestFixtureSetUp]
        public void TestFixtureSetUp() 
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(url);
            db = client.GetServer().GetDatabase(url.DatabaseName);
        }

        [SetUp]
        public void SetUp() 
        {
            sut = new CachedMongoStorage2<MyReadModel, string>(
                collection = new MongoCollection<MyReadModel>(db, "myReadmodelTest", new MongoCollectionSettings() { }));
            collection.Drop();
        }

        [Test]
        public void verify_writing_in_memory_collection()
        {
            sut.EnableCache();
            sut.Insert(new MyReadModel() { Id = "BLABLA" });
            Assert.That(sut.FindOneById("BLABLA"), Is.Not.Null);
            Assert.That(collection.Count(), Is.EqualTo(0));
        }

        [Test]
        public void verify_writing_in_database()
        {
            sut.DisableCache();
            sut.Insert(new MyReadModel() { Id = "BLABLA" });
            Assert.That(sut.FindOneById("BLABLA"), Is.Not.Null);
            Assert.That(collection.Count(), Is.EqualTo(1));
        }

        [Test]
        public void verify_writing_in_memory_then_db()
        {
            sut.EnableCache();
            sut.Insert(new MyReadModel() { Id = "1" });
            sut.DisableCache();
            sut.Insert(new MyReadModel() { Id = "2" });

            Assert.That(collection.Count(), Is.EqualTo(2));
        }

        [Test]
        public void verify_writing_in_memory_then_db_then_re_enable_cache()
        {
            sut.EnableCache();
            sut.Insert(new MyReadModel() { Id = "1" });
            sut.DisableCache();
            sut.Insert(new MyReadModel() { Id = "2" });
            sut.EnableCache();

            Assert.That(sut.FindOneById("2"), Is.Not.Null);
        }

        [Test]
        public void verify_get_non_existent_get()
        {
            sut.EnableCache();
            Assert.That(sut.FindOneById("not_existing"), Is.Null);
        }

        [Test]
        public void verify_autoflush()
        {
            sut.EnableCache();
            sut.Insert(new MyReadModel() { Id = "BLABLA" });
            Assert.That(collection.Count(), Is.EqualTo(0));

            Assert.That(sut.Collection.FindAll().Count(), Is.EqualTo(1));
            Assert.That(collection.Count(), Is.EqualTo(1));
        }

        [Test]
        public void verify_autoflush_then_change_in_mongo()
        {
            sut.EnableCache();
            sut.Insert(new MyReadModel() { Id = "BLABLA", Text = "Original" });
            sut.Flush();
            sut.FindOneById("BLABLA").Text = "Modified";
            sut.Flush();
            var element = collection.FindOneById(BsonValue.Create("BLABLA"));
            Assert.That(element.Text, Is.EqualTo("Modified"));
        }

        [Test]
        public void verify_autoflush_then_delete()
        {
            sut.EnableCache();
            sut.Insert(new MyReadModel() { Id = "BLABLA", Text = "Original" });
            sut.Flush();
            sut.Delete("BLABLA");
            sut.Flush();
            var element = collection.FindOneById(BsonValue.Create("BLABLA"));
            Assert.That(element, Is.Null);
        }

        [Test]
        public void verify_autoflush_then_memory_again()
        {
            sut.EnableCache();
            sut.Insert(new MyReadModel() { Id = "BLABLA" });
            sut.Collection.FindAll(); //this will trigger flush
            sut.Insert(new MyReadModel() { Id = "other_data" });
            Assert.That(collection.Count(), Is.EqualTo(1)); //other_data still in memory
            sut.Flush();
            Assert.That(collection.Count(), Is.EqualTo(2)); //now flushed
        }

        [Test]
        public void stress_multiple_threads()
        {
            Int32 iterationCount = 10000;
            sut.EnableCache();
            Parallel.For(1, 1 + iterationCount, new ParallelOptions() { MaxDegreeOfParallelism = 20 },
                i => {
                 sut.Insert(new MyReadModel() { Id = i.ToString(), Text = "Original" });
                 if (i % 100 == 0) sut.Flush();
            });
            sut.Flush();
            var count = collection.Count();
            Assert.That(count, Is.EqualTo(iterationCount));
        }

        [Test]
        public void verify_autoflush_on_timer()
        {
            sut.EnableCache();
            sut.SetAutoFlush(100);
            sut.Insert(new MyReadModel() { Id = "1" });
            sut.Insert(new MyReadModel() { Id = "2" });
            sut.Insert(new MyReadModel() { Id = "3" });
            Thread.Sleep(200); //Not so good for a test, 
            Assert.That(collection.Count(), Is.EqualTo(3));
        }

        [Test]
        public void verify_autoflush_on_count()
        {
            sut.EnableCache();
            sut.SetAutoFlushOnCount(5);
            sut.Insert(new MyReadModel() { Id = "1" });
            sut.Insert(new MyReadModel() { Id = "2" });
            sut.Insert(new MyReadModel() { Id = "3" });
            sut.Insert(new MyReadModel() { Id = "4" });
            //should not flush
            Assert.That(collection.Count(), Is.EqualTo(0));

            //flush
            sut.Insert(new MyReadModel() { Id = "5" });
            Assert.That(collection.Count(), Is.EqualTo(5));
        }

    }
}

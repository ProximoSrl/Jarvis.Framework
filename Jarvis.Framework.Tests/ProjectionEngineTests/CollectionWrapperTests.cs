using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.SharedTests;
using Jarvis.Framework.Tests.SharedTests.IdentitySupport;
using Jarvis.Framework.Tests.Support;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class CollectionWrapperWithTypedIdTests
    {
        CollectionWrapper<SampleReadModelTestId, TestId> sut;

        IMongoDatabase _db;
        MongoClient _client;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString;
            var url = new MongoUrl(connectionString);
            _client = new MongoClient(url);
            _db = _client.GetDatabase(url.DatabaseName);

            TestHelper.RegisterSerializerForFlatId<TestId>();
        }

        [SetUp]
        public void SetUp()
        {
            _client.DropDatabase(_db.DatabaseNamespace.DatabaseName);
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            sut = new CollectionWrapper<SampleReadModelTestId, TestId>(storageFactory, new NotifyToNobody());
            var projection = new ProjectionTypedId(sut);
        }


        [Test]
        public void verify_basic_delete()
        {
            var rm = new SampleReadModelTestId();
            rm.Id = new TestId(1);
            rm.Value = "test";
            sut.Insert(new SampleAggregateCreated(), rm);
            var all = sut.All.ToList();
            Assert.That(all, Has.Count.EqualTo(1));

            sut.Delete(new SampleAggregateInvalidated(), rm.Id);
            all = sut.All.ToList();
            Assert.That(all, Has.Count.EqualTo(0));
        }

        [Test]
        public void verify_basic_update()
        {
            var rm = new SampleReadModelTestId();
            rm.Id = new TestId(1);
            rm.Value = "test";
            sut.Insert(new SampleAggregateCreated(), rm);
            rm.Value = "test2";
            sut.Save(new SampleAggregateTouched(), rm);
            var all = sut.All.ToList();
            Assert.That(all, Has.Count.EqualTo(1));
            var loaded = all.First();
            Assert.That(loaded.Value, Is.EqualTo("test2"));
        }
    }
}

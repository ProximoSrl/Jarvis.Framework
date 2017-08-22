using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.SharedTests.IdentitySupport;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NUnit.Framework;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class CollectionWrapperTests
    {
        private CollectionWrapper<SampleReadModelTest, TestId> sut;

        private IMongoDatabase _db;
        private MongoClient _client;

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
            sut = new CollectionWrapper<SampleReadModelTest, TestId>(storageFactory, new NotifyToNobody());
            //It is important to create the projection to attach the collection wrapper
            new ProjectionTypedId(sut);
        }

        [Test]
        public async Task Verify_basic_delete()
        {
            var rm = new SampleReadModelTest();
            rm.Id = new TestId(1);
            rm.Value = "test";
            await sut.InsertAsync(new SampleAggregateCreated(), rm);
            var all = sut.All.ToList();
            Assert.That(all, Has.Count.EqualTo(1));

            await sut.DeleteAsync(new SampleAggregateInvalidated(), rm.Id);
            all = sut.All.ToList();
            Assert.That(all, Has.Count.EqualTo(0));
        }

        [Test]
        public async Task Verify_basic_update()
        {
            var rm = new SampleReadModelTest();
            rm.Id = new TestId(1);
            rm.Value = "test";
            await sut.InsertAsync(new SampleAggregateCreated(), rm);
            rm.Value = "test2";
            await sut.SaveAsync(new SampleAggregateTouched(), rm);
            var all = sut.All.ToList();
            Assert.That(all, Has.Count.EqualTo(1));
            var loaded = all.First();
            Assert.That(loaded.Value, Is.EqualTo("test2"));
        }
    }
}

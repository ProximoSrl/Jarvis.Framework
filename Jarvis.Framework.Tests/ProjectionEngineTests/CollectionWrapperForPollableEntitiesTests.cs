using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.SharedTests.IdentitySupport;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Configuration;
using System.Linq;
using Fasterflect;
using Jarvis.Framework.Shared.Events;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class CollectionWrapperForPollableEntitiesTests
    {
        private CollectionWrapper<SampleReadModelPollableTest, TestId> sut;

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
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            sut = new CollectionWrapper<SampleReadModelPollableTest, TestId>(storageFactory, new NotifyToNobody());
            new ProjectionPollableReadmodel(sut);
        }

        [TestFixtureTearDown]
        public void  TestFixtureTearDown()
        {
            _client.DropDatabase(_db.DatabaseNamespace.DatabaseName);
        }

        private Int32 _seed = 0;

        [Test]
        public async Task  Verify_population_of_basic_properties_on_insert()
        {
            var rm = CreateNew();
            SampleAggregateCreated evt = HandleEvent(new SampleAggregateCreated());
            await sut.InsertAsync(evt, rm);
            var reloaded = await sut.FindOneByIdAsync(rm.Id);

            Assert.That(reloaded.CheckpointToken, Is.EqualTo(1L));
            Assert.That(reloaded.SecondaryToken, Is.GreaterThan(0));
        }

        [Test]
        public async Task  Verify_population_of_basic_properties_on_update()
        {
            var rm = CreateNew();
            var evt = HandleEvent(new SampleAggregateCreated());
            await sut.InsertAsync(evt, rm);
            var reloaded = await sut.FindOneByIdAsync(rm.Id);

            rm.Value = "MODIFIED";
            var evt2 = HandleEvent(new SampleAggregateTouched(), 2L);
            await sut.SaveAsync(evt2, rm);

            var reloaded2 = await sut.FindOneByIdAsync(rm.Id);

            Assert.That(reloaded2.CheckpointToken, Is.EqualTo(2L));
            Assert.That(reloaded2.SecondaryToken, Is.GreaterThan(reloaded.SecondaryToken));
        }

        [Test]
        public async Task  Verify_population_of_basic_properties_on_upsert_insert()
        {
            var rm = CreateNew();
            var evt = HandleEvent(new SampleAggregateCreated());
            await sut.UpsertAsync(evt, rm.Id, () => rm, r => r.Value += "MODIFIED");
            var reloaded = await sut.FindOneByIdAsync(rm.Id);

            Assert.That(reloaded.CheckpointToken, Is.EqualTo(1L));
            Assert.That(reloaded.SecondaryToken, Is.GreaterThan(0));
        }

        [Test]
        public async Task  Verify_population_of_basic_properties_on_upsert_update()
        {
            var rm = CreateNew();
            var evt = HandleEvent(new SampleAggregateCreated());
            await sut.InsertAsync(evt, rm);
            var reloaded = await sut.FindOneByIdAsync(rm.Id);

            rm.Value = "MODIFIED";
            var evt2 = HandleEvent(new SampleAggregateTouched(), 2L);
            await sut.UpsertAsync(evt2, rm.Id, () => rm, r => r.Value += "MODIFIED");

            var reloaded2 = await sut.FindOneByIdAsync(rm.Id);

            Assert.That(reloaded2.CheckpointToken, Is.EqualTo(2L));
            Assert.That(reloaded2.SecondaryToken, Is.GreaterThan(reloaded.SecondaryToken));
        }

        [Test]
        public async Task  Verify_population_of_basic_properties_on_find_and_modify()
        {
            var rm = CreateNew();
            var evt = HandleEvent(new SampleAggregateCreated());
            await sut.InsertAsync(evt, rm);
            var reloaded = await sut.FindOneByIdAsync(rm.Id);

            var evt2 = HandleEvent(new SampleAggregateTouched(), 2L);
            await sut.FindAndModifyAsync(evt2, rm.Id, r => r.Value += "MODIFIED", false);

            var reloaded2 = await sut.FindOneByIdAsync(rm.Id);

            Assert.That(reloaded2.CheckpointToken, Is.EqualTo(2L));
            Assert.That(reloaded2.SecondaryToken, Is.GreaterThan(reloaded.SecondaryToken));
        }

        [Test]
        public async Task  Verify_delete_of_readmodel()
        {
            var rm = CreateNew();
            var evt = HandleEvent(new SampleAggregateCreated());
            await sut.InsertAsync(evt, rm);
            await sut.FindOneByIdAsync(rm.Id);

            var evt2 = HandleEvent(new SampleAggregateInvalidated(), 2L);
            await sut.DeleteAsync(evt2, rm.Id);

            var reloaded2 = await sut.FindOneByIdAsync(rm.Id);

            Assert.That(reloaded2, Is.Null, "Readmodel should be deleted if asked to do so");
        }

        public SampleReadModelPollableTest CreateNew()
        {
            var rm = new SampleReadModelPollableTest();
            rm.Id = new TestId(_seed++);
            rm.Value = $"test {_seed}";
            return rm;
        }

        private static T HandleEvent<T>(T evt, Int64 checkpointToken = 1L) where T : DomainEvent
        {
            evt.MessageId = Guid.NewGuid();
            evt.SetPropertyValue("CheckpointToken", checkpointToken);
            return evt;
        }
    }
}

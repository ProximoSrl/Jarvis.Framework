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
        public void TestFixtureTearDown()
        {
            _client.DropDatabase(_db.DatabaseNamespace.DatabaseName);
        }

        private Int32 _seed = 0;

        [Test]
        public void Verify_population_of_basic_properties_on_insert()
        {
            var tick = DateTime.UtcNow.Ticks;
            var rm = CreateNew();
            SampleAggregateCreated evt = HandleEvent(new SampleAggregateCreated());
            sut.Insert(evt, rm);
            var reloaded = sut.FindOneById(rm.Id);

            Assert.That(reloaded.CheckpointToken, Is.EqualTo(1L));
            Assert.That(reloaded.LastUpdatedTicks, Is.GreaterThanOrEqualTo(tick));
            Assert.That(reloaded.SecondaryToken, Is.GreaterThan(0));
        }

        [Test]
        public void Verify_population_of_basic_properties_on_update()
        {
            var rm = CreateNew();
            var evt = HandleEvent(new SampleAggregateCreated());
            sut.Insert(evt, rm);
            var reloaded = sut.FindOneById(rm.Id);
            WaitForTimeToPass(reloaded);

            rm.Value = "MODIFIED";
            var evt2 = HandleEvent(new SampleAggregateTouched(), 2L);
            sut.Save(evt2, rm);

            var reloaded2 = sut.FindOneById(rm.Id);

            Assert.That(reloaded2.CheckpointToken, Is.EqualTo(2L));
            Assert.That(reloaded2.LastUpdatedTicks, Is.GreaterThan(reloaded.LastUpdatedTicks));
            Assert.That(reloaded.SecondaryToken, Is.GreaterThan(0));
        }

        [Test]
        public void Verify_population_of_basic_properties_on_upsert_insert()
        {
            var tick = DateTime.UtcNow.Ticks;
            var rm = CreateNew();
            var evt = HandleEvent(new SampleAggregateCreated());
            sut.Upsert(evt, rm.Id, () => rm, r => r.Value += "MODIFIED");
            var reloaded = sut.FindOneById(rm.Id);

            Assert.That(reloaded.CheckpointToken, Is.EqualTo(1L));
            Assert.That(reloaded.LastUpdatedTicks, Is.GreaterThanOrEqualTo(tick));
            Assert.That(reloaded.SecondaryToken, Is.GreaterThan(0));
        }

        [Test]
        public void Verify_population_of_basic_properties_on_upsert_update()
        {
            var rm = CreateNew();
            var evt = HandleEvent(new SampleAggregateCreated());
            sut.Insert(evt, rm);
            var reloaded = sut.FindOneById(rm.Id);
            WaitForTimeToPass(reloaded);

            rm.Value = "MODIFIED";
            var evt2 = HandleEvent(new SampleAggregateTouched(), 2L);
            sut.Upsert(evt2, rm.Id, () => rm, r => r.Value += "MODIFIED");

            var reloaded2 = sut.FindOneById(rm.Id);

            Assert.That(reloaded2.CheckpointToken, Is.EqualTo(2L));
            Assert.That(reloaded2.LastUpdatedTicks, Is.GreaterThan(reloaded.LastUpdatedTicks));
            Assert.That(reloaded.SecondaryToken, Is.GreaterThan(0));
        }

        [Test]
        public void Verify_population_of_basic_properties_on_find_and_modify()
        {
            var rm = CreateNew();
            var evt = HandleEvent(new SampleAggregateCreated());
            sut.Insert(evt, rm);
            var reloaded = sut.FindOneById(rm.Id);
            WaitForTimeToPass(reloaded);

            var evt2 = HandleEvent(new SampleAggregateTouched(), 2L);
            sut.FindAndModify(evt2, rm.Id, r => r.Value += "MODIFIED", false);

            var reloaded2 = sut.FindOneById(rm.Id);

            Assert.That(reloaded2.CheckpointToken, Is.EqualTo(2L));
            Assert.That(reloaded2.LastUpdatedTicks, Is.GreaterThan(reloaded.LastUpdatedTicks));
            Assert.That(reloaded.SecondaryToken, Is.GreaterThan(0));
        }

        [Test]
        public void Verify_delete_of_readmodel()
        {
            var rm = CreateNew();
            var evt = HandleEvent(new SampleAggregateCreated());
            sut.Insert(evt, rm);
            var reloaded = sut.FindOneById(rm.Id);
            WaitForTimeToPass(reloaded);

            var evt2 = HandleEvent(new SampleAggregateInvalidated(), 2L);
            sut.Delete(evt2, rm.Id);

            var reloaded2 = sut.FindOneById(rm.Id);

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

        private static void WaitForTimeToPass(SampleReadModelPollableTest reloaded)
        {
            while (DateTime.UtcNow.Ticks == reloaded.LastUpdatedTicks)
            {
                //Do something.
            }
        }
    }
}

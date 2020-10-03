using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicCollectionWrapperRealPersistenceTests : AtomicCollectionWrapperTestsBase
    {
        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            Init();
        }

        [SetUp]
        public void SetUp()
        {
            InitSingleTest();
        }

        [Test]
        public async Task Auto_reload_on_obsolete_reamodel()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            Assert.That(rm.TouchCount, Is.EqualTo(1));
            await _sut.UpsertAsync( rm).ConfigureAwait(false);

            //ok now in db we have an older version, lets update version
            SimpleTestAtomicReadModel.FakeSignature = 2;

            //I want to reload and autocorrect, reprojecting again everything.
            GenerateSut();
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded.TouchCount, Is.EqualTo(2));
            Assert.That(reloaded.ReadModelVersion, Is.EqualTo(2));
        }

        [Test]
        public async Task Auto_reload_on_serialization_error()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            Assert.That(rm.TouchCount, Is.EqualTo(1));
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //ok now go to the database and alter the document
            var doc = _mongoBsonCollection.FindOneById(rm.Id);
            doc ["ExtraProperty"] = 42;
            _mongoBsonCollection.ReplaceOne(
                Builders<BsonDocument>.Filter.Eq("_id", rm.Id),
                doc);

            //I want to reload and autocorrect, reprojecting again everything.
            GenerateSut();
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded.TouchCount, Is.EqualTo(1));
            Assert.That(reloaded.ReadModelVersion, Is.EqualTo(1));
        }

        [Test]
        public async Task Auto_reload_on_multiple_obsolete_reamodels()
        {
            SimpleTestAtomicReadModel rm1 = GenerateChangesetWithTwoEvents();
            SimpleTestAtomicReadModel rm2 = GenerateChangesetWithTwoEvents();
            Assert.That(rm1.TouchCount, Is.EqualTo(1));
            Assert.That(rm2.TouchCount, Is.EqualTo(1));
            await _sut.UpsertAsync(rm1).ConfigureAwait(false);
            await _sut.UpsertAsync(rm2).ConfigureAwait(false);

            //ok now in db we have an older version, lets update version
            SimpleTestAtomicReadModel.FakeSignature = 2;

            //I want to reload and autocorrect, reprojecting again everything.
            GenerateSut();
            var reloaded = await _sut.FindManyAsync(_ => _.Id == rm1.Id || _.Id==rm2.Id, true).ConfigureAwait(false);

            Assert.That(reloaded, Is.Not.Null);
            var rmList = reloaded.ToList();
            Assert.That(rmList.Count, Is.EqualTo(2));
            Assert.That(rmList[0].TouchCount, Is.EqualTo(2));
            Assert.That(rmList[1].TouchCount, Is.EqualTo(2));
            Assert.That(rmList[0].ReadModelVersion, Is.EqualTo(2));
            Assert.That(rmList[1].ReadModelVersion, Is.EqualTo(2));
        }

        [Test]
        public async Task Read_without_reload_on_multiple_obsolete_reamodels()
        {
            SimpleTestAtomicReadModel rm1 = GenerateChangesetWithTwoEvents();
            SimpleTestAtomicReadModel rm2 = GenerateChangesetWithTwoEvents();
            Assert.That(rm1.TouchCount, Is.EqualTo(1));
            Assert.That(rm2.TouchCount, Is.EqualTo(1));
            await _sut.UpsertAsync(rm1).ConfigureAwait(false);
            await _sut.UpsertAsync(rm2).ConfigureAwait(false);

            //ok now in db we have an older version, lets update version
            SimpleTestAtomicReadModel.FakeSignature = 2;

            //I want to reload and autocorrect, reprojecting again everything.
            GenerateSut();
            var reloaded = await _sut.FindManyAsync(_ => _.Id == rm1.Id || _.Id == rm2.Id, false).ConfigureAwait(false);

            Assert.That(reloaded, Is.Not.Null);
            var rmList = reloaded.ToList();
            Assert.That(rmList.Count, Is.EqualTo(2));
            Assert.That(rmList[0].TouchCount, Is.EqualTo(1));
            Assert.That(rmList[1].TouchCount, Is.EqualTo(1));
            Assert.That(rmList[0].ReadModelVersion, Is.EqualTo(1));
            Assert.That(rmList[1].ReadModelVersion, Is.EqualTo(1));
        }

        private SimpleTestAtomicReadModel GenerateChangesetWithTwoEvents()
        {
            _aggregateIdSeed++;
            var id = new SampleAggregateId(_aggregateIdSeed);
            var rm1 = new SimpleTestAtomicReadModel(id);
            rm1.ProcessChangeset(GenerateCreatedEvent(false));
            rm1.ProcessChangeset(GenerateTouchedEvent(false));
            return rm1;
        }

        /// <summary>
        /// This is something strange, if we have still someone that needs old 
        /// readmodel, but some new readmodel is present, we wanto to in-memory
        /// autorebuild, but not writing to database
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task Auto_reload_from_obsolete_reader_can_still_unwind()
        {
            SimpleTestAtomicReadModel.FakeSignature = 2;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //go backward
            SimpleTestAtomicReadModel.FakeSignature = 1;

            //I want to reload and autocorrect, reprojecting again everything.
            GenerateSut();
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded.TouchCount, Is.EqualTo(1));
            Assert.That(reloaded.ReadModelVersion, Is.EqualTo(1)); //signature change

            //no update should be done to the DB
            var record = _collection.AsQueryable().Single(_ => _.Id == rm.Id);
            Assert.That(record.TouchCount, Is.EqualTo(2));
            Assert.That(record.ReadModelVersion, Is.EqualTo(2));
        }

        [Test]
        public async Task Auto_reload_save_record_on_db()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            await _sut.UpsertAsync( rm).ConfigureAwait(false);

            //ok now in db we have an older version, lets update version
            SimpleTestAtomicReadModel.FakeSignature = 2;

            //I want to reload and autocorrect, reprojecting again everything.
            GenerateSut();
            await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);

            var record = _collection.AsQueryable().Single(_ => _.Id == rm.Id);
            Assert.That(record.TouchCount, Is.EqualTo(2));
            Assert.That(record.ReadModelVersion, Is.EqualTo(2));
        }
    }
}

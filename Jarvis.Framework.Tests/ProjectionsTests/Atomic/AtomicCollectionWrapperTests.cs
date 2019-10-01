using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using MongoDB.Driver;
using NStore.Core.Persistence;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicCollectionWrapperTests : AtomicCollectionWrapperTestsBase
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

        #region Standard tests

        [Test]
        public async Task Verify_basic_insert_of_readmodel()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //We need to check that the readmodel was saved correctly
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            var evt = changeset.Events[0] as DomainEvent;
            Assert.That(reloaded.Id, Is.EqualTo(evt.AggregateId.AsString()));
        }

        [Test]
        public async Task Verify_idempotency_on_save_older_model()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //now generate another touch and save
            var changesetTouch2 = GenerateTouchedEvent(false);
            rm.ProcessChangeset(changesetTouch2);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //check
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.TouchCount, Is.EqualTo(2));

            //ok create a readmodel with less event
            var rm2 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm2.ProcessChangeset(evt);
            rm2.ProcessChangeset(evtTouch);
            await _sut.UpsertAsync(rm2).ConfigureAwait(false);

            //reload the readmdoel, the version should never be overwritten with old version
            reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.TouchCount, Is.EqualTo(2));
            var evtTouch2 = changesetTouch2.Events[0] as DomainEvent;
            Assert.That(reloaded.ProjectedPosition, Is.EqualTo(evtTouch2.CheckpointToken));

            //redo again without the insert mode
            await _sut.UpdateAsync(rm2).ConfigureAwait(false);

            //reload the readmdoel, the version should never be overwritten with old version
            reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.TouchCount, Is.EqualTo(2));
            Assert.That(reloaded.ProjectedPosition, Is.EqualTo(evtTouch2.CheckpointToken));
        }

        [Test]
        public async Task Verify_idempotency_insert()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //now try to resave with insert true, but without any change, no exception
            //should be raised.
            await _sut.UpsertAsync(rm).ConfigureAwait(false);
        }

        [Test]
        public async Task Verify_force_insert_bypass_equal_version()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //saved at version 2, manually alter data on database.
            var rmLoaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            rmLoaded.SetPropertyValue(_ => _.TouchCount, 10000);
            _collection.Save(rmLoaded, rm.Id);

            //now try to resave with insert true, but without any change, no exception, no data changed
            await _sut.UpsertAsync(rm).ConfigureAwait(false);
            rmLoaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(rmLoaded.TouchCount, Is.EqualTo(10000));

            //now upsert with force true, this will update data in database no matter what.
            await _sut.UpsertForceAsync(rm).ConfigureAwait(false);
            rmLoaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(rmLoaded.TouchCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Verify_force_insert_honors_readmodel_version()
        {
            //Create a readmodel with signature equalto 2.
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            SimpleTestAtomicReadModel.FakeSignature = 2;
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //Now alter data on database
            var rmLoaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            rmLoaded.SetPropertyValue(_ => _.TouchCount, 10000);
            _collection.Save(rmLoaded, rm.Id);

            //Act: change the signature, downgrade the model
            rm.SetPropertyValue(_ => _.ReadModelVersion, 1);

            //now try to resave with insert true, but without any change, no exception, no data changed
            await _sut.UpsertAsync(rm).ConfigureAwait(false);
            rmLoaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(rmLoaded.TouchCount, Is.EqualTo(10000));

            //now upsert with force true, this should NOT update the reamodel because the version is lower.
            await _sut.UpsertForceAsync(rm).ConfigureAwait(false);
            rmLoaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(rmLoaded.TouchCount, Is.EqualTo(10000));
        }

        [Test]
        public async Task Verify_idempotency_on_old_version()
        {
            SimpleTestAtomicReadModel.FakeSignature = 2;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //now try to save another RM but with a version that is lower
            //it should not happen, but we need to keep this 100% foolproof.
            SimpleTestAtomicReadModel.FakeSignature = 1;
            var rm2 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm2.ProcessChangeset(evt);
            rm2.ProcessChangeset(evtTouch);
            var evtTouch2 = GenerateTouchedEvent(false);
            rm2.ProcessChangeset(evtTouch2); //now the versino of the aggregate is greater

            await _sut.UpdateAsync(rm2).ConfigureAwait(false);
            //reload the readmdoel, the version should never be overwritten with old version
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.TouchCount, Is.EqualTo(2));

            var evt2 = evtTouch2.Events[0] as DomainEvent;
            Assert.That(reloaded.ProjectedPosition, Is.EqualTo(evt2.CheckpointToken));

            //Same test, but with insert mode true
            await _sut.UpsertAsync(rm2).ConfigureAwait(false);
            //reload the readmdoel, the version should never be overwritten with old version
            reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.TouchCount, Is.EqualTo(2));
            Assert.That(reloaded.ProjectedPosition, Is.EqualTo(evt2.CheckpointToken));
        }

        [Test]
        public async Task Verify_new_signature_overwrite_readmodel_even_with_same_version()
        {
            SimpleTestAtomicReadModel.FakeSignature = 1;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var changesetTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(changesetTouch);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //Retry saving without checking with same version but higher signature.
            SimpleTestAtomicReadModel.FakeSignature = 2;
            var rm2 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm2.ProcessChangeset(evt);
            rm2.ProcessChangeset(changesetTouch);
            Assert.That(rm2.TouchCount, Is.EqualTo(2));

            await _sut.UpdateAsync(rm2).ConfigureAwait(false);

            //reload the readmdoel, the version should be overwritten because signature is new
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.TouchCount, Is.EqualTo(2));
            Assert.That(reloaded.ReadModelVersion, Is.EqualTo(2));

            var evtTouch = changesetTouch.Events[0] as DomainEvent;
            Assert.That(reloaded.ProjectedPosition, Is.EqualTo(evtTouch.CheckpointToken));
        }

        [Test]
        public async Task Process_extra_changeset_make_readmodel_not_persistable()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);
            //check
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.TouchCount, Is.EqualTo(1));

            //now generate another touch event but process with extra events
            var changesetTouch2 = GenerateTouchedEvent(false);
            rm.ProcessExtraStreamChangeset(changesetTouch2);
            Assert.That(rm.TouchCount, Is.EqualTo(2));
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //we expect two things.
            Assert.That(rm.ModifiedWithExtraStreamEvents, Is.True, "After process extra stream changeset the aggregate is not persistable");

            //saving should not have persisted
            reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.TouchCount, Is.EqualTo(1));
            Assert.That(reloaded.ProjectedPosition, Is.EqualTo(evtTouch.GetChunkPosition()));
        }

        [Test]
        public async Task Not_persistale_is_honored_in_update()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);
            //check
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.TouchCount, Is.EqualTo(1));

            //forcing not persistable
            rm.SetPropertyValue(_ => _.ModifiedWithExtraStreamEvents, true);

            //saving should not have persisted
            reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.TouchCount, Is.EqualTo(1));
            Assert.That(reloaded.ProjectedPosition, Is.EqualTo(evtTouch.GetChunkPosition()));
        }

        [Test]
        public void Before_and_after_called()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var firstEvt = evt.Events[0] as DomainEvent;

            Assert.That(rm.ExtraString, Is.EqualTo($"B-{firstEvt.MessageId}IN-{firstEvt.MessageId}A-{firstEvt.MessageId}"));
        }

        [Test]
        public void Before_and_after_not_called_for_events_not_handled()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var commit1 = GenerateCreatedEvent(false);
            rm.ProcessChangeset(commit1);
            var firstEvt = commit1.Events[0] as DomainEvent;

            var commit2 = GenerateInvalidatedEvent(false);
            rm.ProcessChangeset(commit2);

            Assert.That(rm.ExtraString, Is.EqualTo($"B-{firstEvt.MessageId}IN-{firstEvt.MessageId}A-{firstEvt.MessageId}"));
        }

        [Test]
        public async Task Not_persistable_is_honored_in_insert()
        {
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var evt = GenerateCreatedEvent(false);
            rm.ProcessChangeset(evt);
            var evtTouch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(evtTouch);

            //forcing not persistable
            rm.SetPropertyValue(_ => _.ModifiedWithExtraStreamEvents, true);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            //check
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Null);
        }

        #endregion

        #region Infrastructure tests

        [Test]
        public async Task Can_be_resolved_with_factory()
        {
            using (WindsorContainer container = new WindsorContainer())
            {
                container.AddFacility<JarvisTypedFactoryFacility>();
                container.Register(
                    Component
                        .For<IMongoDatabase>()
                        .Instance(_db),
                    Component
                        .For<IPersistence>()
                        .Instance(_persistence),
                    Component
                        .For<IIdentityManager, IIdentityConverter>()
                        .Instance(_identityManager),
                     Component
                        .For<ICommitEnhancer>()
                        .ImplementedBy<CommitEnhancer>(),
                    Component
                        .For<ILiveAtomicReadModelProcessor>()
                        .ImplementedBy<LiveAtomicReadModelProcessor>(),
                    Component
                        .For<IAtomicReadModelFactory>()
                        .ImplementedBy<AtomicReadModelFactory>(),
                    Component
                        .For(new Type[]
                        {
                            typeof (IAtomicMongoCollectionWrapper<>),
                            typeof (IAtomicCollectionWrapper<>),
                            typeof (IAtomicCollectionReader<>),
                        })
                        .ImplementedBy(typeof(AtomicMongoCollectionWrapper<>))
                        .Named("AtomicMongoCollectionWrapper"),
                      Component
                        .For<IAtomicCollectionWrapperFactory>()
                        .AsFactory()
                    );

                var factory = container.Resolve<IAtomicCollectionWrapperFactory>();

                var cw = factory.CreateCollectionWrappper<SimpleTestAtomicReadModel>();
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var evt = GenerateCreatedEvent(false);
                rm.ProcessChangeset(evt);
                await cw.UpsertAsync(rm).ConfigureAwait(false);
            }
        }

        #endregion
    }
}


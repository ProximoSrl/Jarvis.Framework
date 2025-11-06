using Castle.Core.Logging;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Kernel.ProjectionEngine;
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

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic;

[TestFixture]
public class AtomicCollectionWrapperTests : AtomicCollectionWrapperTestsBase
{
    [SetUp]
    public void SetUp()
    {
        Init();
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
        Assert.DoesNotThrowAsync(() => _sut.UpsertAsync(rm));
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
    public async Task Ability_to_catchup_events()
    {
        //Arrange create the readmodel, and process some events, then persist those events.
        var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
        var evt = GenerateCreatedEvent(false);
        rm.ProcessChangeset(evt);
        var evtTouch = GenerateTouchedEvent(false);
        rm.ProcessChangeset(evtTouch);

        //do not forget to persist the readmodel
        await _sut.UpsertAsync(rm).ConfigureAwait(false);

        //now generate another event that is not projected
        var extraTouchEvent = GenerateTouchedEvent(false);

        //Check
        //first of all verify that standard read does not read last event.
        var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);

        Assert.That(reloaded, Is.Not.Null);
        Assert.That(reloaded.AggregateVersion, Is.EqualTo(evtTouch.AggregateVersion));
        var actualTouchCount = reloaded.TouchCount;

        //now we need to load with catchup
        var reloadedWithCatchup = await _sut.FindOneByIdAndCatchupAsync(rm.Id).ConfigureAwait(false);

        //Assert
        Assert.That(reloadedWithCatchup, Is.Not.Null);
        Assert.That(reloadedWithCatchup.AggregateVersion, Is.EqualTo(extraTouchEvent.AggregateVersion));
        Assert.That(reloadedWithCatchup.TouchCount, Is.EqualTo(actualTouchCount + 1));
    }

    [Test]
    public async Task Ability_to_catchup_events_when_nothing_was_still_projected()
    {
        //Arrange create the readmodel, and process some events, then persist those events.
        var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
        GenerateCreatedEvent(false);
        var lastEvent = GenerateTouchedEvent(false);

        //Check
        //first of all verify that standard read does not read last event.
        var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
        Assert.That(reloaded, Is.Null);

        //now we need to load with catchup
        var reloadedWithCatchup = await _sut.FindOneByIdAndCatchupAsync(rm.Id).ConfigureAwait(false);

        //Assert
        Assert.That(reloadedWithCatchup, Is.Not.Null);
        Assert.That(reloadedWithCatchup.AggregateVersion, Is.EqualTo(lastEvent.AggregateVersion));
        Assert.That(reloadedWithCatchup.TouchCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Ability_to_catchup_events_return_null_if_aggregate_is_not_present()
    {
        var reloadedWithCatchup = await _sut.FindOneByIdAndCatchupAsync(new SampleAggregateId(123456789)).ConfigureAwait(false);

        //Assert
        Assert.That(reloadedWithCatchup, Is.Null);
    }

    [Test]
    public async Task Ability_to_project_At_checkpoint_is_not_present()
    {
        var reloadedWithCatchup = await _sut.FindOneByIdAtCheckpointAsync(new SampleAggregateId(123456789), 100).ConfigureAwait(false);

        //Assert 
        Assert.That(reloadedWithCatchup, Is.Null, "Non existing entity");
    }

    [Test]
    public async Task Ability_to_project_at_checkpoint()
    {
        //Arrange create the readmodel, and process some events, then persist those events.
        var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
        GenerateCreatedEvent(false);
        var first = GenerateTouchedEvent(false);
        var second = GenerateTouchedEvent(false);

        var firstCheckpoint = first.GetChunkPosition();
        var secondCheckpoint = second.GetChunkPosition();

        //Check
        var reloaded = await _sut.FindOneByIdAtCheckpointAsync(rm.Id, firstCheckpoint).ConfigureAwait(false);
        Assert.That(reloaded.TouchCount, Is.EqualTo(1), $"At checkpoint {firstCheckpoint} we expect correct value");

        reloaded = await _sut.FindOneByIdAtCheckpointAsync(rm.Id, secondCheckpoint).ConfigureAwait(false);
        Assert.That(reloaded.TouchCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Project_at_checkpoint_where_object_does_not_exists()
    {
        //Arrange create the readmodel, and process some events, then persist those events.
        var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
        GenerateCreatedEvent(false);
        var first = GenerateTouchedEvent(false);
        var second = GenerateTouchedEvent(false);

        var firstCheckpoint = first.GetChunkPosition();
        var secondCheckpoint = second.GetChunkPosition();

        //Check
        var reloaded = await _sut.FindOneByIdAtCheckpointAsync(rm.Id, 0).ConfigureAwait(false);
        Assert.That(reloaded, Is.Null);
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

        var commit2 = GenerateSampleAggregateNotHandledEvent(false);
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

    [Test]
    public async Task Verify_update_when_aggregate_is_not_changed()
    {
        var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
        var changeset = GenerateCreatedEvent(false);
        rm.ProcessChangeset(changeset);
        await _sut.UpsertAsync(rm).ConfigureAwait(false);

        //ok now we process a changeset with events that are not processed
        var newChangeset = GenerateInvalidatedEvent(false);
        rm.ProcessChangeset(newChangeset);

        //Check that the event is applied
        Assert.That(newChangeset.AggregateVersion, Is.EqualTo(changeset.AggregateVersion + 1));
        Assert.That(rm.AggregateVersion, Is.EqualTo(newChangeset.AggregateVersion));
        Assert.That(rm.ProjectedPosition, Is.EqualTo(newChangeset.GetChunkPosition()));

        //ok now we want to upgrade the record partially
        await _sut.UpdateVersionAsync(rm);

        //ASSERT CHeck
        var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
        Assert.That(reloaded, Is. Not.Null);
        Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion));
        Assert.That(reloaded.ProjectedPosition, Is.EqualTo(rm.ProjectedPosition));
        Assert.That(reloaded.LastProcessedVersions, Is.EquivalentTo(new long[] { 1, 2 }));
    }

    [Test]
    public async Task Verify_update_when_aggregate_does_not_handle_first_event()
    {
        var rm = new SimpleTestAtomicReadModelPrivate(new SampleAggregateId(_aggregateIdSeed));

        var anotherSut = new AtomicMongoCollectionWrapper<SimpleTestAtomicReadModelPrivate>(
           _db,
           new AtomicReadModelFactory(),
           new LiveAtomicReadModelProcessor(new AtomicReadModelFactory(), new CommitEnhancer(), _persistence),
           NullLogger.Instance);

        var changeset = GenerateCreatedEvent(false);
        NUnit.Framework.Legacy.ClassicAssert.False( rm.ProcessChangeset(changeset));
        await anotherSut.UpdateVersionAsync(rm).ConfigureAwait(false);

        //ASSERT CHeck
        var reloaded = await anotherSut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
        Assert.That(reloaded, Is.Not.Null);
        Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion));
        Assert.That(reloaded.ProjectedPosition, Is.EqualTo(rm.ProjectedPosition));
        Assert.That(reloaded.LastProcessedVersions, Is.EquivalentTo(new long[] { 1 }));
    }

    #endregion

    #region Reading tests

    /// <summary>
    /// tests are executed in a single-server mongodb environment.
    /// </summary>
    /// <returns></returns>
    [Test]
    public async Task Can_read_on_secondary_not_error_single_server()
    {
        //save a readmodel
        var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
        var evt = GenerateCreatedEvent(false);
        rm.ProcessChangeset(evt);
        var evtTouch = GenerateTouchedEvent(false);
        rm.ProcessChangeset(evtTouch);
        await _sut.UpsertAsync(rm).ConfigureAwait(false);

        //check
        var reloaded = _sut.AsQueryableSecondaryPreferred()
            .Single(r => r.Id == rm.Id);

        Assert.That(reloaded.TouchCount, Is.EqualTo(rm.TouchCount));
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
                    .For<ILogger>()
                    .Instance(NullLogger.Instance),
                Component
                    .For(new Type[]
                    {
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

    #region Delete tests

    [Test]
    public async Task Verify_delete_removes_readmodel()
    {
        // Arrange: Create and save a readmodel
        var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
        var changeset = GenerateCreatedEvent(false);
        rm.ProcessChangeset(changeset);
        await _sut.UpsertAsync(rm).ConfigureAwait(false);

        // Verify it exists
        var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
        Assert.That(reloaded, Is.Not.Null);

        // Act: Delete the readmodel
        await _sut.DeleteAsync(rm.Id).ConfigureAwait(false);

        // Assert: Verify it no longer exists
        var afterDelete = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
        Assert.That(afterDelete, Is.Null);
    }

    [Test]
    public async Task Verify_delete_with_nonexistent_id_does_not_throw()
    {
        // Act & Assert: Deleting non-existent readmodel should not throw
        Assert.DoesNotThrowAsync(() => _sut.DeleteAsync(new SampleAggregateId(999999).AsString()));
    }

    [Test]
    public void Verify_delete_with_null_id_throws_exception()
    {
        // Act & Assert: Deleting with null id should throw
        var ex = Assert.ThrowsAsync<CollectionWrapperException>(() => _sut.DeleteAsync(null));
        Assert.That(ex.Message, Does.Contain("Id is null or empty"));
    }

    [Test]
    public void Verify_delete_with_empty_id_throws_exception()
    {
        // Act & Assert: Deleting with empty id should throw
        var ex = Assert.ThrowsAsync<CollectionWrapperException>(() => _sut.DeleteAsync(string.Empty));
        Assert.That(ex.Message, Does.Contain("Id is null or empty"));
    }

    [Test]
    public async Task Verify_delete_multiple_readmodels()
    {
        // Arrange: Create and save multiple readmodels
        _aggregateIdSeed = 100;
        var rm1 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
        var changeset1 = GenerateCreatedEvent(false);
        rm1.ProcessChangeset(changeset1);
        await _sut.UpsertAsync(rm1).ConfigureAwait(false);

        _aggregateIdSeed = 200;
        _aggregateVersion = 1;
        var rm2 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
        var changeset2 = GenerateCreatedEvent(false);
        rm2.ProcessChangeset(changeset2);
        await _sut.UpsertAsync(rm2).ConfigureAwait(false);

        // Verify both exist
        var reloaded1 = await _sut.FindOneByIdAsync(rm1.Id).ConfigureAwait(false);
        var reloaded2 = await _sut.FindOneByIdAsync(rm2.Id).ConfigureAwait(false);
        Assert.That(reloaded1, Is.Not.Null);
        Assert.That(reloaded2, Is.Not.Null);

        // Act: Delete first readmodel
        await _sut.DeleteAsync(rm1.Id).ConfigureAwait(false);

        // Assert: Verify first is deleted but second still exists
        var afterDelete1 = await _sut.FindOneByIdAsync(rm1.Id).ConfigureAwait(false);
        var afterDelete2 = await _sut.FindOneByIdAsync(rm2.Id).ConfigureAwait(false);
        Assert.That(afterDelete1, Is.Null);
        Assert.That(afterDelete2, Is.Not.Null);
    }

    #endregion

    #region private classes

    private class SimpleTestAtomicReadModelPrivate : AbstractAtomicReadModel
    {
        public SimpleTestAtomicReadModelPrivate(string id) : base(id)
        {
        }

        public bool Invalidated { get; private set; }

        protected override int GetVersion()
        {
            return 1;
        }

        public void On(SampleAggregateInvalidated _)
        {
            Invalidated = true;  
        }
    }

    #endregion
}
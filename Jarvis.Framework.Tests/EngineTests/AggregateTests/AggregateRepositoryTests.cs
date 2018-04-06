using Castle.Windsor;
using Jarvis.Framework.Kernel.Engine;
using NStore.Core.InMemory;
using NStore.Core.Snapshots;
using NStore.Core.Streams;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.EngineTests.AggregateTests
{
    [TestFixture]
    public class AggregateRepositoryTests
    {
        private Repository sut;
        private WindsorContainer container;
        private InMemoryPersistence memoryPersistence;
        private ISnapshotStore snapshotStore;

        [SetUp]
        public void SetUp()
        {
            container = new WindsorContainer();
            memoryPersistence = new InMemoryPersistence();
            sut = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence));

            var snapshotMemoryPersistence = new InMemoryPersistence();
            snapshotStore = new DefaultSnapshotStore(snapshotMemoryPersistence);
        }

        [TearDown]
        public void TearDown()
        {
            container.Dispose();
        }

        [Test]
        public async Task Verify_repository_restore_inner_entities()
        {
            var aggregate = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            aggregate.Touch();
            aggregate.SampleEntity.AddValue(10);
            await sut.SaveAsync(aggregate, Guid.NewGuid().ToString()).ConfigureAwait(false);

            //Real test should use another repository to verify re-hydratation of internal state.
            var otherRepo = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence));
            var reloaded = await otherRepo.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            Assert.That(reloaded.SampleEntity.InternalState.Accumulator, Is.EqualTo(10));
        }

        [Test]
        public async Task Verify_repository_restore_inner_entities_with_snapshot()
        {
            sut = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence), snapshotStore);
            var aggregate = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            aggregate.Touch();
            aggregate.SampleEntity.AddValue(10);
            await sut.SaveAsync(aggregate, Guid.NewGuid().ToString()).ConfigureAwait(false);

            //Real test should use another repository to verify re-hydratation of internal state.
            var otherRepo = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence), snapshotStore);
            var reloaded = await otherRepo.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            Assert.That(reloaded.SampleEntity.InternalState.Accumulator, Is.EqualTo(10));
        }

        [Test]
        public async Task Verify_repository_dispatch_extra_events_when_restored_with_snapshot()
        {
            sut = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence), snapshotStore);
            var aggregate = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            aggregate.Touch();
            aggregate.SampleEntity.AddValue(10);
            await sut.SaveAsync(aggregate, Guid.NewGuid().ToString()).ConfigureAwait(false); //Save snapshot version 1

            //repo without snapshot
            var otherRepo  = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence));
            aggregate = await otherRepo.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            aggregate.Touch();
            aggregate.SampleEntity.AddValue(10);
            await otherRepo.SaveAsync(aggregate, Guid.NewGuid().ToString()).ConfigureAwait(false); //does not save snapshot

            //not another repository, this time with snapshot manager
            otherRepo = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence), snapshotStore);
            var reloaded = await otherRepo.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            Assert.That(reloaded.Version, Is.EqualTo(2));
            Assert.That(reloaded.WasRestoredFromSnapshot);
            Assert.That(reloaded.RestoreSnapshot.SourceVersion, Is.EqualTo(1), "Entity should be restored from snapshot 20");

            Assert.That(reloaded.SampleEntity.InternalState.Accumulator, Is.EqualTo(20));
            Assert.That(reloaded.InternalState.TouchCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Verify_repository_dispatch_extra_events_only_with_entity_events_when_restored_with_snapshot()
        {
            sut = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence), snapshotStore);
            var aggregate = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            aggregate.Touch();
            aggregate.SampleEntity.AddValue(10);
            await sut.SaveAsync(aggregate, Guid.NewGuid().ToString()).ConfigureAwait(false); //Save snapshot version 1

            //repo without snapshot
            var otherRepo = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence));
            aggregate = await otherRepo.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            //IMPORTANT, DO NOT CALL ROOT ENTITY METHODS, WE WANT AN EXTRA COMMIT WITH ONLY EVENTS FROM ENTITY.
            aggregate.SampleEntity.AddValue(10);
            await otherRepo.SaveAsync(aggregate, Guid.NewGuid().ToString()).ConfigureAwait(false); //does not save snapshot

            //not another repository, this time with snapshot manager
            otherRepo = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence), snapshotStore);
            var reloaded = await otherRepo.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            Assert.That(reloaded.Version, Is.EqualTo(2));
            Assert.That(reloaded.WasRestoredFromSnapshot);
            Assert.That(reloaded.RestoreSnapshot.SourceVersion, Is.EqualTo(1), "Entity should be restored from snapshot 20");

            Assert.That(reloaded.SampleEntity.InternalState.Accumulator, Is.EqualTo(20));
            Assert.That(reloaded.InternalState.TouchCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Verify_aggregate_State_invariants_check_invariants_of_entity_states()
        {
            var aggregate = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            aggregate.Touch();
            aggregate.SampleEntity.AddValue(42);
            var entityInvariants = aggregate.SampleEntity.InternalState.CheckInvariants();
            Assert.That(entityInvariants.IsInvalid);
            var invariants = aggregate.InternalState.CheckInvariants();
            Assert.That(invariants.IsInvalid);
        }

        [Test]
        public async Task Verify_repository_save_checks_entity_state_invariant()
        {
            var aggregate = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            aggregate.Touch();
            aggregate.SampleEntity.AddValue(42);
            Assert.ThrowsAsync<InvariantCheckFailedException>(() =>
                sut.SaveAsync(aggregate, Guid.NewGuid().ToString()));
        }

        [Test]
        public async Task Verify_extension_to_load_with_type()
        {
            var aggregateGeneric = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            Assert.That(aggregateGeneric, Is.InstanceOf<AggregateTestSampleAggregate1>());

            var aggregate = await sut.GetByIdAsync(typeof(AggregateTestSampleAggregate1), "AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            Assert.That(aggregate, Is.InstanceOf<AggregateTestSampleAggregate1>());
            Assert.That(aggregate.Id, Is.EqualTo("AggregateTestSampleAggregate1_42"));
        }

        [Test]
        public async Task Verify_extension_to_Save_with_type()
        {
            Type type = typeof(AggregateTestSampleAggregate1);
            var aggregate = (AggregateTestSampleAggregate1)await sut.GetByIdAsync(type, "AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            aggregate.Touch();
            await sut.SaveAsync(aggregate, Guid.NewGuid().ToString(), accessor => accessor.Add("HEADER", "VALUE")).ConfigureAwait(false);

            var reloaded = (AggregateTestSampleAggregate1)await sut.GetByIdAsync(type, "AggregateTestSampleAggregate1_42").ConfigureAwait(false);
            Assert.That(reloaded.InternalState.TouchCount, Is.EqualTo(1));
        }
    }
}

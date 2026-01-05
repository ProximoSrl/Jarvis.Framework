using Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicReadmodelProjectorHelperBatchTests : AtomicProjectionEngineTestBase
    {
        [Test]
        public async Task HandleManyAsync_creates_readmodels_for_multiple_identities()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var cs1 = await ProcessEvent(new SampleAggregateCreated(), p => new SampleAggregateId(1001)).ConfigureAwait(false);
            var cs2 = await ProcessEvent(new SampleAggregateCreated(), p => new SampleAggregateId(1002)).ConfigureAwait(false);

            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(cs1.GetChunkPosition(), cs1, cs1.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs2.GetChunkPosition(), cs2, cs2.GetIdentity())
            };

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            Assert.That(results.Count, Is.EqualTo(2));

            var rm1 = await _collection.FindOneByIdAsync(((DomainEvent)cs1.Events[0]).AggregateId).ConfigureAwait(false);
            var rm2 = await _collection.FindOneByIdAsync(((DomainEvent)cs2.Events[0]).AggregateId).ConfigureAwait(false);

            Assert.That(rm1, Is.Not.Null);
            Assert.That(rm2, Is.Not.Null);
            Assert.That(rm1.ProjectedPosition, Is.EqualTo(((DomainEvent)cs1.Events[0]).CheckpointToken));
            Assert.That(rm2.ProjectedPosition, Is.EqualTo(((DomainEvent)cs2.Events[0]).CheckpointToken));
        }

        [Test]
        public void HandleManyAsync_throws_when_duplicate_identities_in_batch()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            // Use same identity for both commits
            var id = new SampleAggregateId(2001);
            var cs1 = ProcessEvent(new SampleAggregateCreated(), p => id).Result;
            var cs2 = ProcessEvent(new SampleAggregateTouched(), p => id).Result;

            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(cs1.GetChunkPosition(), cs1, cs1.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs2.GetChunkPosition(), cs2, cs2.GetIdentity())
            };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await sut.HandleManyAsync(items).ConfigureAwait(false));

            Assert.That(ex.Message, Does.Contain("HandleManyAsync does not support multiple changesets for the same identity"));
            Assert.That(ex.Message, Does.Contain("SampleAggregate_2001"));
        }

        [Test]
        public async Task HandleManyAsync_handles_empty_collection()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var items = new List<AtomicReadmodelProjectionItem>();

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleManyAsync_filters_out_wrong_aggregate_type()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            // Create an event for a different aggregate type
            var atomicCs = await ProcessEvent(new AtomicAggregateCreated(), p => new AtomicAggregateId(3001)).ConfigureAwait(false);
            var sampleCs = await ProcessEvent(new SampleAggregateCreated(), p => new SampleAggregateId(3002)).ConfigureAwait(false);

            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(atomicCs.GetChunkPosition(), atomicCs, atomicCs.GetIdentity()),
                new AtomicReadmodelProjectionItem(sampleCs.GetChunkPosition(), sampleCs, sampleCs.GetIdentity())
            };

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            // Only the SampleAggregate event should be processed
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Readmodel.Id, Is.EqualTo(((DomainEvent)sampleCs.Events[0]).AggregateId));
        }

        [Test]
        public async Task HandleManyAsync_handles_exceptions_during_projection()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var id1 = new SampleAggregateId(4001);
            var id2 = new SampleAggregateId(4002);

            var cs1 = await ProcessEvent(new SampleAggregateCreated(), p => id1).ConfigureAwait(false);

            // Set touch max to 0 to trigger exception on touch
            SimpleTestAtomicReadModel.TouchMax = 0;
            var cs2 = await ProcessEvent(new SampleAggregateCreated(), p => id2).ConfigureAwait(false);
            var cs3 = await ProcessEvent(new SampleAggregateTouched(), p => id2).ConfigureAwait(false);

            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(cs1.GetChunkPosition(), cs1, cs1.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs3.GetChunkPosition(), cs3, cs3.GetIdentity())
            };

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            Assert.That(results.Count, Is.EqualTo(2));

            var rm1 = await _collection.FindOneByIdAsync(((DomainEvent)cs1.Events[0]).AggregateId).ConfigureAwait(false);
            var rm2 = await _collection.FindOneByIdAsync(((DomainEvent)cs3.Events[0]).AggregateId).ConfigureAwait(false);

            Assert.That(rm1, Is.Not.Null);
            Assert.That(rm1.Faulted, Is.False, "First readmodel should not be faulted");
            Assert.That(rm2, Is.Not.Null);
            Assert.That(rm2.Faulted, Is.True, "Second readmodel should be marked as faulted after exception");
        }

        [Test]
        public async Task HandleManyAsync_handles_multiple_exceptions_in_batch()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var id1 = new SampleAggregateId(4101);
            var id2 = new SampleAggregateId(4102);
            var id3 = new SampleAggregateId(4103);
            var id4 = new SampleAggregateId(4104);

            var cs1 = await ProcessEvent(new SampleAggregateCreated(), p => id1).ConfigureAwait(false);
            var cs2 = await ProcessEvent(new SampleAggregateCreated(), p => id2).ConfigureAwait(false);

            SimpleTestAtomicReadModel.TouchMax = 0;
            var cs3 = await ProcessEvent(new SampleAggregateCreated(), p => id3).ConfigureAwait(false);
            var cs4Touch = await ProcessEvent(new SampleAggregateTouched(), p => id3).ConfigureAwait(false);
            var cs4 = await ProcessEvent(new SampleAggregateCreated(), p => id4).ConfigureAwait(false);
            var cs5Touch = await ProcessEvent(new SampleAggregateTouched(), p => id4).ConfigureAwait(false);

            // We can only have one changeset per identity, so let's test with changesets that will fault
            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(cs1.GetChunkPosition(), cs1, cs1.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs2.GetChunkPosition(), cs2, cs2.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs4Touch.GetChunkPosition(), cs4Touch, cs4Touch.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs5Touch.GetChunkPosition(), cs5Touch, cs5Touch.GetIdentity())
            };

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            Assert.That(results.Count, Is.EqualTo(4));

            var rm1 = await _collection.FindOneByIdAsync(((DomainEvent)cs1.Events[0]).AggregateId).ConfigureAwait(false);
            var rm2 = await _collection.FindOneByIdAsync(((DomainEvent)cs2.Events[0]).AggregateId).ConfigureAwait(false);
            var rm3 = await _collection.FindOneByIdAsync(((DomainEvent)cs4Touch.Events[0]).AggregateId).ConfigureAwait(false);
            var rm4 = await _collection.FindOneByIdAsync(((DomainEvent)cs5Touch.Events[0]).AggregateId).ConfigureAwait(false);

            Assert.That(rm1.Faulted, Is.False, "First readmodel should not be faulted");
            Assert.That(rm2.Faulted, Is.False, "Second readmodel should not be faulted");
            Assert.That(rm3.Faulted, Is.True, "Third readmodel should be faulted");
            Assert.That(rm4.Faulted, Is.True, "Fourth readmodel should be faulted");
        }

        [Test]
        public async Task HandleManyAsync_handles_changesets_with_multiple_events()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var id = new SampleAggregateId(5001);

            // Create a changeset with multiple events
            var events = new DomainEvent[]
            {
                new SampleAggregateCreated(),
                new SampleAggregateTouched(),
                new SampleAggregateTouched()
            };
            var cs = await ProcessEvents(events, p => id).ConfigureAwait(false);

            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(cs.GetChunkPosition(), cs, cs.GetIdentity())
            };

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            Assert.That(results.Count, Is.EqualTo(1));

            var rm = await _collection.FindOneByIdAsync(((DomainEvent)cs.Events[0]).AggregateId).ConfigureAwait(false);
            Assert.That(rm, Is.Not.Null);
            Assert.That(rm.Created, Is.True);
            Assert.That(rm.TouchCount, Is.EqualTo(2), "Should have applied both touch events");
        }

        [Test]
        public async Task HandleManyAsync_handles_four_or_more_different_identities()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var id1 = new SampleAggregateId(6001);
            var id2 = new SampleAggregateId(6002);
            var id3 = new SampleAggregateId(6003);
            var id4 = new SampleAggregateId(6004);

            var cs1 = await ProcessEvent(new SampleAggregateCreated(), p => id1).ConfigureAwait(false);
            var cs2 = await ProcessEvent(new SampleAggregateCreated(), p => id2).ConfigureAwait(false);
            var cs3 = await ProcessEvent(new SampleAggregateCreated(), p => id3).ConfigureAwait(false);
            var cs4 = await ProcessEvent(new SampleAggregateCreated(), p => id4).ConfigureAwait(false);

            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(cs1.GetChunkPosition(), cs1, cs1.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs2.GetChunkPosition(), cs2, cs2.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs3.GetChunkPosition(), cs3, cs3.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs4.GetChunkPosition(), cs4, cs4.GetIdentity())
            };

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            Assert.That(results.Count, Is.EqualTo(4));

            var rm1 = await _collection.FindOneByIdAsync(((DomainEvent)cs1.Events[0]).AggregateId).ConfigureAwait(false);
            var rm2 = await _collection.FindOneByIdAsync(((DomainEvent)cs2.Events[0]).AggregateId).ConfigureAwait(false);
            var rm3 = await _collection.FindOneByIdAsync(((DomainEvent)cs3.Events[0]).AggregateId).ConfigureAwait(false);
            var rm4 = await _collection.FindOneByIdAsync(((DomainEvent)cs4.Events[0]).AggregateId).ConfigureAwait(false);

            Assert.That(rm1, Is.Not.Null);
            Assert.That(rm2, Is.Not.Null);
            Assert.That(rm3, Is.Not.Null);
            Assert.That(rm4, Is.Not.Null);
        }

        [Test]
        public async Task HandleManyAsync_processes_large_batch_efficiently()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var items = new List<AtomicReadmodelProjectionItem>();
            const int batchSize = 100;

            // Create 100 different aggregates
            for (int i = 0; i < batchSize; i++)
            {
                var id = new SampleAggregateId(8000 + i);
                var cs = await ProcessEvent(new SampleAggregateCreated(), p => id).ConfigureAwait(false);
                items.Add(new AtomicReadmodelProjectionItem(cs.GetChunkPosition(), cs, cs.GetIdentity()));
            }

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            Assert.That(results.Count, Is.EqualTo(batchSize));

            // Verify a few random ones
            var rm1 = await _collection.FindOneByIdAsync(new SampleAggregateId(8000).AsString()).ConfigureAwait(false);
            var rm50 = await _collection.FindOneByIdAsync(new SampleAggregateId(8050).AsString()).ConfigureAwait(false);
            var rm99 = await _collection.FindOneByIdAsync(new SampleAggregateId(8099).AsString()).ConfigureAwait(false);

            Assert.That(rm1, Is.Not.Null);
            Assert.That(rm50, Is.Not.Null);
            Assert.That(rm99, Is.Not.Null);
        }

        [Test]
        public async Task HandleManyAsync_handles_changeset_with_invalidated_event()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var id1 = new SampleAggregateId(9001);
            var id2 = new SampleAggregateId(9002);

            // Create a changeset with both Created and Invalidated events
            var events = new DomainEvent[]
            {
                new SampleAggregateCreated(),
                new SampleAggregateInvalidated()
            };
            var cs1 = await ProcessEvents(events, p => id1).ConfigureAwait(false);
            var cs2 = await ProcessEvent(new SampleAggregateCreated(), p => id2).ConfigureAwait(false);

            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(cs1.GetChunkPosition(), cs1, cs1.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs2.GetChunkPosition(), cs2, cs2.GetIdentity())
            };

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            Assert.That(results.Count, Is.EqualTo(2));

            var rm1 = await _collection.FindOneByIdAsync(((DomainEvent)cs1.Events[0]).AggregateId).ConfigureAwait(false);
            var rm2 = await _collection.FindOneByIdAsync(((DomainEvent)cs2.Events[0]).AggregateId).ConfigureAwait(false);

            Assert.That(rm1, Is.Not.Null);
            Assert.That(rm1.Created, Is.True);
            Assert.That(rm1.Invalidated, Is.True);
            Assert.That(rm2, Is.Not.Null);
            Assert.That(rm2.Created, Is.True);
            Assert.That(rm2.Invalidated, Is.False);
        }

        [Test]
        public async Task HandleManyAsync_with_single_item_behaves_like_Handle()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var id = new SampleAggregateId(10001);
            var cs = await ProcessEvent(new SampleAggregateCreated(), p => id).ConfigureAwait(false);

            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(cs.GetChunkPosition(), cs, cs.GetIdentity())
            };

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].CreatedForFirstTime, Is.True);

            var rm = await _collection.FindOneByIdAsync(((DomainEvent)cs.Events[0]).AggregateId).ConfigureAwait(false);
            Assert.That(rm, Is.Not.Null);
            Assert.That(rm.Created, Is.True);
        }

        [Test]
        public async Task HandleManyAsync_correctly_sets_CreatedForFirstTime_flag()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var id1 = new SampleAggregateId(11001);
            var id2 = new SampleAggregateId(11002);
            var id3 = new SampleAggregateId(11003);

            var cs1 = await ProcessEvent(new SampleAggregateCreated(), p => id1).ConfigureAwait(false);
            var cs2 = await ProcessEvent(new SampleAggregateCreated(), p => id2).ConfigureAwait(false);

            // Manually insert id3 to database so it already exists
            await sut.Handle(1, await ProcessEvent(new SampleAggregateCreated(), p => id3).ConfigureAwait(false), id3).ConfigureAwait(false);

            var cs3 = await ProcessEvent(new SampleAggregateTouched(), p => id3).ConfigureAwait(false); // Update existing

            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(cs1.GetChunkPosition(), cs1, cs1.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs2.GetChunkPosition(), cs2, cs2.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs3.GetChunkPosition(), cs3, cs3.GetIdentity())
            };

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            Assert.That(results.Count, Is.EqualTo(3));

            var createdResults = results.Where(r => r.CreatedForFirstTime).ToList();
            var updatedResults = results.Where(r => !r.CreatedForFirstTime).ToList();

            Assert.That(createdResults.Count, Is.EqualTo(2), "Two readmodels should be created for first time");
            Assert.That(updatedResults.Count, Is.EqualTo(1), "One readmodel should be updated");
        }
    }
}

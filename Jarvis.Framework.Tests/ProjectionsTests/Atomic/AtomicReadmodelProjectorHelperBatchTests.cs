using Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NStore.Domain;
using NUnit.Framework;
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
        public async Task HandleManyAsync_applies_multiple_changesets_for_same_identity_in_order()
        {
            var sut = _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            // Use same identity for both commits
            var id = new SampleAggregateId(2001);
            var cs1 = await ProcessEvent(new SampleAggregateCreated(), p => id).ConfigureAwait(false);
            var cs2 = await ProcessEvent(new SampleAggregateTouched(), p => id).ConfigureAwait(false);

            var items = new List<AtomicReadmodelProjectionItem>
            {
                new AtomicReadmodelProjectionItem(cs1.GetChunkPosition(), cs1, cs1.GetIdentity()),
                new AtomicReadmodelProjectionItem(cs2.GetChunkPosition(), cs2, cs2.GetIdentity())
            };

            var results = (await sut.HandleManyAsync(items).ConfigureAwait(false)).ToList();

            // Only second changeset modifies the readmodel (touch), first creates
            Assert.That(results.Count, Is.EqualTo(2));

            var rm = await _collection.FindOneByIdAsync(((DomainEvent)cs1.Events[0]).AggregateId).ConfigureAwait(false);
            Assert.That(rm, Is.Not.Null);
            Assert.That(rm.TouchCount, Is.EqualTo(1));
            Assert.That(rm.ProjectedPosition, Is.EqualTo(((DomainEvent)cs2.Events[0]).CheckpointToken));
        }
    }
}

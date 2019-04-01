using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class LiveAtomicReadModelProcessorTests : AtomicProjectionEngineTestBase
    {
        [Test]
        public async Task Project_distinct_entities()
        {
            var c1 = await GenerateSomeEvents().ConfigureAwait(false);

            _aggregateIdSeed++;
            var c2 = await GenerateSomeEvents().ConfigureAwait(false);
            await GenerateTouchedEvent(false).ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(((DomainEvent)c1.Events[0]).AggregateId, Int32.MaxValue).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(2));

            processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(((DomainEvent)c2.Events[0]).AggregateId, Int32.MaxValue).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(3));
        }

        [Test]
        public async Task Project_up_until_certain_aggregate_version()
        {
            var c1 = await GenerateSomeEvents().ConfigureAwait(false);

            var c2 = await GenerateTouchedEvent(false).ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(((DomainEvent)c1.Events[0]).AggregateId, c1.AggregateVersion).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(2));

            processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(((DomainEvent)c1.Events[0]).AggregateId, c2.AggregateVersion).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(3));
        }

        [Test]
        public async Task Project_up_until_certain_checkpoint_number()
        {
            var c1 = await GenerateSomeEvents().ConfigureAwait(false);

            var c2 = await GenerateTouchedEvent(false).ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            DomainEvent firstEvent = (DomainEvent)c1.Events[0];
            var processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                firstEvent.CheckpointToken).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(2));
            Assert.That(processed.AggregateVersion, Is.EqualTo(3));

            firstEvent = (DomainEvent)c2.Events[0];
            processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                  firstEvent.AggregateId.AsString(),
                  firstEvent.CheckpointToken).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(3));
            Assert.That(processed.AggregateVersion, Is.EqualTo(4));
        }
    }
}

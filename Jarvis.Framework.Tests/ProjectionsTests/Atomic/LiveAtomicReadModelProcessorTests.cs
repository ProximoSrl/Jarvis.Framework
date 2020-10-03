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
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            _aggregateIdSeed++;
            var c2 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

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
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            var c2 = await GenerateTouchedEvent().ConfigureAwait(false);

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
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c2 = await GenerateTouchedEvent().ConfigureAwait(false);

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

        [Test]
        public async Task Capability_of_catchup_events()
        {
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c2 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c3 = await GenerateTouchedEvent().ConfigureAwait(false);

            //Arrange: manually process some events in readmodel
            var firstEvent = (DomainEvent)c2.Events[0];
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var rm = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                c2.AggregateVersion).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c2.AggregateVersion));
            var touchCount = rm.TouchCount;

            //Act, ask to catchup events.       
            await sut.CatchupAsync(rm).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c3.AggregateVersion));
            Assert.That(rm.TouchCount, Is.EqualTo(touchCount + 1));
        }

        [Test]
        public async Task Cacthup_events_with_no_more_Events_works()
        {
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c2 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            //Arrange: manually process some events in readmodel
            var firstEvent = (DomainEvent)c2.Events[0];
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var rm = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                c2.AggregateVersion).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c2.AggregateVersion));
            var touchCount = rm.TouchCount;

            //Act, ask to catchup events.       
            await sut.CatchupAsync(rm).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c2.AggregateVersion));
            Assert.That(rm.TouchCount, Is.EqualTo(touchCount));
        }

        [Test]
        public async Task Capability_of_catchup_to_project_everything()
        {
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c3 = await GenerateTouchedEvent().ConfigureAwait(false);

            //Arrange: manually process some events in readmodel
            var firstEvent = (DomainEvent) c3.Events[0];
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var rm = new SimpleTestAtomicReadModel(firstEvent.AggregateId);

            //Act, ask to catchup events.       
            await sut.CatchupAsync(rm).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c3.AggregateVersion));
            Assert.That(rm.TouchCount, Is.EqualTo(3)); //we have 3 touch events.
        }
    }
}

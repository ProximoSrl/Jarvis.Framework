using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class ResiliencyAtomicProjectionTests : AtomicProjectionEngineTestBase
    {
        [Test]
        public async Task Verify_exceptions_are_handled_for_specific_instance_of_readmodel()
        {
            SimpleTestAtomicReadModel.TouchMax = 1; //we want at maximum one touch.

            //first step, create two touch events, the second one generates a problem
            Changeset changeset = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            _aggregateIdSeed++; //start working on another aggregate
            var secondAggregateChangeset = await GenerateCreatedEvent().ConfigureAwait(false);

            //And finally check if everything is projected
            var sut = await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);

            //we need to wait to understand if it was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

            //First readmodel have only one touch
            var evt = changeset.Events[0] as DomainEvent;
            var rm = await _collection.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);
            Assert.That(rm.TouchCount, Is.EqualTo(1));
            Assert.That(rm.Faulted);

            //but the second aggregate should be projected.
            evt = secondAggregateChangeset.Events[0] as DomainEvent;
            rm = await _collection.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);
            Assert.That(rm.ProjectedPosition, Is.EqualTo(evt.CheckpointToken));
            Assert.That(rm.Created, Is.EqualTo(true));
            Assert.That(rm.TouchCount, Is.EqualTo(0));

            //another event
            var anotherchangeset = await GenerateTouchedEvent().ConfigureAwait(false);

            //we need to wait to understand if it was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

            await sut.StopAsync().ConfigureAwait(false);

            rm = await _collection.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);
            evt = anotherchangeset.Events[0] as DomainEvent;
            Assert.That(rm.ProjectedPosition, Is.EqualTo(evt.CheckpointToken));
            Assert.That(rm.Created, Is.EqualTo(true));
            Assert.That(rm.TouchCount, Is.EqualTo(1));
        }
    }
}
namespace Jarvis.Framework.Tests.EngineTests.AggregateTests
{
    [TestFixture]
    public class BasicEventRoutingTests
    {
        private AggregateTestSampleAggregate1 sut;

        [SetUp]
        public void SetUp()
        {
            sut = new AggregateTestSampleAggregate1();
            sut.Init("AggregateTestSampleAggregate1_42");
        }

        [Test]
        public void Can_route_to_internal_State()
        {
            sut.Touch();
            Assert.That(sut.InternalState.TouchCount, Is.EqualTo(1), "simple raise event did not get routed to the state");
        }

        [Test]
        public void Allow_external_raising_of_events_from_entity_root()
        {
            sut.SampleEntity.AddValue(10);
            Changeset changeset = ((IEventSourcedAggregate)sut).GetChangeSet();
            Assert.That(changeset.Events.Length, Is.EqualTo(1));
            var evt = (AggregateTestSampleEntityAddValue)changeset.Events[0];
            Assert.That(evt.AggregateId.AsString(), Is.EqualTo(sut.Id));
        }

        [Test]
        public void Events_raised_from_entity_should_be_dispatched_to_entity_state()
        {
            sut.SampleEntity.AddValue(10);
            Assert.That(sut.SampleEntity.InternalState.Accumulator, Is.EqualTo(10), "Missing update of entity internal state");
        }

        [Test]
        public void Events_raised_from_aggregate_should_be_dispatched_to_entity()
        {
            sut.DoubleTouch();
            Assert.That(sut.SampleEntity.AggregateEvents.Count, Is.EqualTo(2), "Event of aggregate was not routed to the entity");
        }
    }
}

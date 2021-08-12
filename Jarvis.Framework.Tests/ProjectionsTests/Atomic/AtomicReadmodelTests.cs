using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicReadmodelTests : AtomicProjectionEngineTestBase
    {
        [Test]
        public async Task Verify_basic_dispatch_of_changeset()
        {
            var cs = await GenerateCreatedEvent().ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            Assert.That(rm.ProjectedPosition, Is.EqualTo(cs.GetChunkPosition()));
        }

        [Test]
        public async Task Verify_correctly_return_of_handled_event()
        {
            var cs = await GenerateCreatedEvent().ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            Assert.IsTrue(rm.Created);
        }

        [Test]
        public async Task Verify_cache_does_not_dispatch_wrong_event()
        {
            //ok process a created event
            var cs = await GenerateCreatedEvent().ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            //then take a different readmodel that does not consume that element
            ++_aggregateIdSeed;
            var rm2 = new SimpleAtomicAggregateReadModel(new AtomicAggregateId(_aggregateIdSeed));
            var processed = rm2.ProcessChangeset(cs);

            Assert.IsFalse(processed);
        }

        [Test]
        public async Task Verify_cache_does_not_dispatch_wrong_event_to_reamodel_of_same_aggregate()
        {
            //ok process a created event
            var cs = await GenerateCreatedEvent().ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            //then take a different readmodel that does not consume that element
            ++_aggregateIdSeed;
            var rm2 = new AnotherSimpleTestAtomicReadModel(new AtomicAggregateId(_aggregateIdSeed));
            var processed = rm2.ProcessChangeset(cs);

            Assert.IsTrue(processed);
            Assert.That(rm2.Created, Is.True);
        }

        [Test]
        public async Task Verify_correctly_return_of_handled_event_even_if_a_single_Event_is_handled()
        {
            var cs = await GenerateCreatedEvent().ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            var evta = new SampleAggregateDerived1();
            var evtb = new SampleAggregateTouched();
            var cs2 = await ProcessEvents(new DomainEvent[] { evta, evtb }, p => new AtomicAggregateId(p)).ConfigureAwait(false);
            Assert.IsTrue(rm.ProcessChangeset(cs2));
        }

        [Test]
        public async Task Verify_correctly_return_false_of_Unhandled_event()
        {
            var cs = await GenerateSampleAggregateDerived1().ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            Assert.IsFalse(rm.ProcessChangeset(cs));
        }

        [Test]
        public async Task Verify_basic_dispatch_of_two_changeset()
        {
            var cs = await GenerateAtomicAggregateCreatedEvent().ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);
            var touch = await GenerateTouchedEvent().ConfigureAwait(false);
            rm.ProcessChangeset(touch);

            Assert.That(rm.ProjectedPosition, Is.EqualTo(touch.GetChunkPosition()));
        }

        [Test]
        public async Task Verify_idempotence_of_changeest_processing()
        {
            var cs = await GenerateAtomicAggregateCreatedEvent().ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);
            var touch = await GenerateTouchedEvent().ConfigureAwait(false);
            rm.ProcessChangeset(touch);

            Assert.That(rm.TouchCount, Is.EqualTo(1));

            rm.ProcessChangeset(touch);

            Assert.That(rm.TouchCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Verify_idempotence_of_changeest_processing_past_event()
        {
            var cs = await GenerateAtomicAggregateCreatedEvent().ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);
            var touch = await GenerateTouchedEvent().ConfigureAwait(false);
            rm.ProcessChangeset(touch);

            Assert.That(rm.TouchCount, Is.EqualTo(1));

            var touch2 = await GenerateTouchedEvent().ConfigureAwait(false);
            rm.ProcessChangeset(touch2);
            //reprocess past event.
            rm.ProcessChangeset(touch);

            Assert.That(rm.TouchCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Verify_processing_event_with_holes()
        {
            var cs = await GenerateAtomicAggregateCreatedEvent().ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);
            var touch1 = await GenerateTouchedEvent().ConfigureAwait(false);
            var touch2 = await GenerateTouchedEvent().ConfigureAwait(false);

            rm.ProcessChangeset(touch2);
            Assert.That(rm.TouchCount, Is.EqualTo(1), "We can process event even if there are holes.");

            rm.ProcessChangeset(touch1);
            Assert.That(rm.TouchCount, Is.EqualTo(1), "Past events should not be processed");
        }

        [Test]
        public async Task Verify_basic_properties()
        {
            var cs = await GenerateCreatedEvent(issuedBy : "admin").ConfigureAwait(false);
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);

            Assert.That(rm.CreationUser, Is.EqualTo("admin"));
            Assert.That(rm.LastModificationUser, Is.EqualTo("admin"));
            Assert.That(rm.LastModify, Is.EqualTo(((DomainEvent)cs.Events[0]).CommitStamp));
            Assert.That(rm.AggregateVersion, Is.EqualTo(cs.AggregateVersion));

            cs = await GenerateTouchedEvent(issuedBy: "admin2").ConfigureAwait(false);
            rm.ProcessChangeset(cs);

            Assert.That(rm.CreationUser, Is.EqualTo("admin"));
            Assert.That(rm.LastModificationUser, Is.EqualTo("admin2"));
            Assert.That(rm.LastModify, Is.EqualTo(((DomainEvent)cs.Events[0]).CommitStamp));
            Assert.That(rm.AggregateVersion, Is.EqualTo(cs.AggregateVersion));
        }

        [Test]
        public async Task When_event_is_not_handled_reamodel_should_not_marked_as_changed()
        {
            var cs = await GenerateCreatedEvent(issuedBy: "admin").ConfigureAwait(false);
            var rm = new SimpleAtomicReadmodelWithSingleEventHandled(new SampleAggregateId(_aggregateIdSeed));

            Assert.That(rm.AggregateVersion, Is.EqualTo(0), "Uninitialized Atomic reamodel should have 0 as version");

            var processed = rm.ProcessChangeset(cs);
            Assert.That(processed, Is.False, "That readmodel does not process Created event so it should communicate that the event is not handled");

            cs = await GenerateTouchedEvent(issuedBy: "admin2").ConfigureAwait(false);
            processed = rm.ProcessChangeset(cs);

            Assert.IsTrue(processed, "Touched event should be processed");
        }
    }
}

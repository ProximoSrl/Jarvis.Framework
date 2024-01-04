using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NUnit.Framework;
using System.Collections.Generic;
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
		public async Task Verify_track_of_version_processed()
		{
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			rm.ProcessChangeset(cs);

			Assert.That(rm.LastProcessedVersions, Is.EquivalentTo(new[] { 1 }));
		}

        [Test]
        public async Task Verify_track_of_version_processed_when_event_is_not_handled()
        {
            var cs = await GenerateCreatedEvent().ConfigureAwait(false);
            var touch1 = await GenerateTouchedEvent().ConfigureAwait(false);
            var invalidated = await GenerateInvalidatedEvent().ConfigureAwait(false);
            var touch2 = await GenerateTouchedEvent().ConfigureAwait(false);

            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm.ProcessChangeset(cs);
            rm.ProcessChangeset(touch1);
            rm.ProcessChangeset(invalidated);
            Assert.That(rm.LastProcessedVersions, Is.EquivalentTo(new[] { 1,2,3, }));

            rm.ProcessChangeset(touch2);
            Assert.That(rm.LastProcessedVersions, Is.EquivalentTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
		public async Task Verify_track_of_version_processed_maintain_last_events()
		{
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			rm.ProcessChangeset(cs);

			for (int i = 0; i < AbstractAtomicReadModel.MaxNumberOfVersionToKeep + 10; i++)
			{
				cs = await GenerateTouchedEvent();
				rm.ProcessChangeset(cs);
			}

			Assert.That(rm.LastProcessedVersions.Count, Is.EqualTo(AbstractAtomicReadModel.MaxNumberOfVersionToKeep));

			List<long> expectedSequence = new List<long>();
			long actualVersion = cs.AggregateVersion;
			while (expectedSequence.Count < AbstractAtomicReadModel.MaxNumberOfVersionToKeep)
			{
				expectedSequence.Add(actualVersion--);
			}

			//Verify exact sequence, ordered from less to most recent
			expectedSequence.Reverse();
			Assert.That(rm.LastProcessedVersions, Is.EqualTo(expectedSequence));
		}

		[Test]
		public async Task Verify_correctly_return_of_handled_event()
		{
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			rm.ProcessChangeset(cs);

			NUnit.Framework.Legacy.ClassicAssert.IsTrue(rm.Created);
		}

		//[Test]
		//public async Task Verify_cache_does_not_dispatch_wrong_event()
		//{
		//	//ok process a created event
		//	var cs = await GenerateCreatedEvent().ConfigureAwait(false);
		//	var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		//	rm.ProcessChangeset(cs);

		//	//then take a different readmodel that does not consume that element
		//	++_aggregateIdSeed;
		//	var rm2 = new SimpleAtomicAggregateReadModel(new AtomicAggregateId(_aggregateIdSeed));
		//	var processed = rm2.ProcessChangeset(cs);

		//	NUnit.Framework.Legacy.ClassicAssert.IsFalse(processed);
		//}

		[Test]
		public async Task Verify_cache_does_not_dispatch_wrong_event_to_reamodel_of_same_aggregate()
		{
			//ok process a created event
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			rm.ProcessChangeset(cs);

			//Generate another id, then let second readmodel process a changeset that is not 
			//meant to be consumed by that aggregate.
			++_aggregateIdSeed;
			var rm2 = new AnotherSimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			Assert.Throws<JarvisFrameworkEngineException>(() => rm2.ProcessChangeset(cs));
		}

		[Test]
		public async Task Verify_correctly_return_of_handled_event_even_if_a_single_Event_is_handled()
		{
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			rm.ProcessChangeset(cs);

			var evta = new SampleAggregateDerived1();
			var evtb = new SampleAggregateTouched();
			var cs2 = await ProcessEvents(new DomainEvent[] { evta, evtb }, p => new SampleAggregateId(p)).ConfigureAwait(false);
			NUnit.Framework.Legacy.ClassicAssert.IsTrue(rm.ProcessChangeset(cs2));
		}

		[Test]
		public async Task Verify_correctly_return_false_of_Unhandled_event()
		{
			var cs = await GenerateSampleAggregateDerived1().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			NUnit.Framework.Legacy.ClassicAssert.IsFalse(rm.ProcessChangeset(cs));
		}

		[Test]
		public async Task Verify_basic_dispatch_of_two_changeset()
		{
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(cs.GetIdentity().AsString());
			rm.ProcessChangeset(cs);
			var touch = await GenerateTouchedEvent().ConfigureAwait(false);
			rm.ProcessChangeset(touch);

			Assert.That(rm.ProjectedPosition, Is.EqualTo(touch.GetChunkPosition()));
		}

		[Test]
		public async Task Verify_idempotence_of_changeest_processing()
		{
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			rm.ProcessChangeset(cs);
			var touch = await GenerateTouchedEvent().ConfigureAwait(false);
			rm.ProcessChangeset(touch);

			Assert.That(rm.TouchCount, Is.EqualTo(1));

			var processed = rm.ProcessChangeset(touch);

			Assert.That(processed, Is.False);
			Assert.That(rm.TouchCount, Is.EqualTo(1));
		}

		[Test]
		public async Task Verify_idempotence_of_changeset_processing_past_event()
		{
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
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

		/// <summary>
		/// <para>
		/// This is somewhat controversial test, if we were to redispatch a past event
		/// we must ignore it only if it was correctly handled, or we can create problem.
		/// </para>
		/// <para>
		/// We have three events dispatched in order 1, 3, 2 => no error will be raised, 
		/// event 2 will be ignored.
		/// </para>
		/// </summary>
		/// <returns></returns>
		[Test]
		public async Task Verify_processing_event_with_holes()
		{
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			rm.ProcessChangeset(cs);
			var event_empty_generated_to_increment_counter = await GenerateTouchedEvent().ConfigureAwait(false);
			var touch2 = await GenerateTouchedEvent().ConfigureAwait(false);

			//Suppose touch1 event is simply missing so it will not be dispatched because it is empty
			//we need to be able to continue processing events.
			rm.ProcessChangeset(touch2);
			Assert.That(rm.TouchCount, Is.EqualTo(1), "We can process event even if there are holes.");
			Assert.That(rm.AggregateVersion, Is.EqualTo(3), "Aggregate has an hole, no problem");
		}

		[Test]
		public async Task Verify_dispatching_in_wrong_order()
		{
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			rm.ProcessChangeset(cs);
			var touch1 = await GenerateTouchedEvent().ConfigureAwait(false);
			var touch2 = await GenerateTouchedEvent().ConfigureAwait(false);

			//we dispatch touch2 events, like in the event of an hole
			rm.ProcessChangeset(touch2);
			Assert.That(rm.TouchCount, Is.EqualTo(1), "We can process event even if there are holes.");

			//Then we try to re-dispatch older event, we need to raise exception
			Assert.Throws<JarvisFrameworkEngineException>(() => rm.ProcessChangeset(touch1), "Dispatch old events that was not present in the list");
		}

		/// <summary>
		/// We keep a certain number of version processed in LastProcessedVersions property, but
		/// when this information become stale, we tolerate re-dispatch.
		/// </summary>
		/// <returns></returns>
		[Test]
		public async Task Verify_dispatching_in_wrong_order_tolerate_skew()
		{
			var cs = await GenerateCreatedEvent().ConfigureAwait(false);
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			rm.ProcessChangeset(cs);
			var touch1 = await GenerateTouchedEvent().ConfigureAwait(false);

			//Now process more event that we can keep in memory in the readmodel.
			for (int i = 0; i < AbstractAtomicReadModel.MaxNumberOfVersionToKeep + 1; i++)
			{
				var evt = await GenerateTouchedEvent().ConfigureAwait(false);
				rm.ProcessChangeset(evt);
			}

			//Then we try to re-dispatch older event, we need to raise exception
			Assert.DoesNotThrow(() => rm.ProcessChangeset(touch1), "Tolerate undispatched event because is really old");
		}

		[Test]
		public async Task Verify_basic_properties()
		{
			var cs = await GenerateCreatedEvent(issuedBy: "admin").ConfigureAwait(false);
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

			NUnit.Framework.Legacy.ClassicAssert.IsTrue(processed, "Touched event should be processed");
		}

		[Test]
		public async Task Tolerate_dispatching_wrong_aggregate_events()
		{
			var cs = await GenerateCreatedEvent(issuedBy: "admin").ConfigureAwait(false);
			var rm = new SimpleSubAtomicAggregateReadModel(new SubAtomicAggregateId(_aggregateIdSeed));

			//we are processing a changeset that is of another stream, but the readmodel has no way to 
			//understand that this is a commit that does not belongs to him. 
			Assert.Throws<JarvisFrameworkEngineException>(() => rm.ProcessChangeset(cs));
		}

		[Test]
		public async Task Tolerate_dispatching_different_id_event()
		{
			var cs1 = await GenerateCreatedEvent(issuedBy: "admin").ConfigureAwait(false);
			_aggregateIdSeed++;
			var cs2 = await GenerateCreatedEvent(issuedBy: "admin").ConfigureAwait(false);

			var rm = new SimpleSubAtomicAggregateReadModel(cs1.GetIdentity().AsString());

			Assert.DoesNotThrow(() => rm.ProcessChangeset(cs1));

			Assert.That(rm.AggregateVersion, Is.EqualTo(1L));

			//we are processing a changeset that is of another stream, but the readmodel has no way to 
			//understand that this is a commit that does not belongs to him. 
			Assert.Throws<JarvisFrameworkEngineException>(() => rm.ProcessChangeset(cs2));
		}
	}
}

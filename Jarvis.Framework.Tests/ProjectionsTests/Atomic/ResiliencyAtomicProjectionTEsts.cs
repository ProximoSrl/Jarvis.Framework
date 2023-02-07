using Fasterflect;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

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
			Assert.That(rm.AggregateVersion, Is.EqualTo(3)); //Exception is thrown during third event so we have version 3 processed with error

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

		[Test]
		public async Task Verify_framework_internal_exception_can_be_automatically_fixed()
		{
			//Simulate generation of an internal exception, 
			SimpleTestAtomicReadModel.TouchMax = 1; //we want at maximum one touch to generate exception
			SimpleTestAtomicReadModel.GenerateInternalExceptionforMaxTouch = true;

			//first step, create two touch events, the second one generates a problem
			Changeset changeset = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

			//And finally check if everything is projected
			await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);

			//we need to wait to understand if it was projected
			GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

			//First readmodel have only one touch
			SimpleTestAtomicReadModel.TouchMax = Int32.MaxValue;
			var evt = changeset.Events[0] as DomainEvent;
			var wrapper = _container.Resolve<IAtomicCollectionWrapper<SimpleTestAtomicReadModel>>();
			var rm = await wrapper.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);

			Assert.That(rm.Faulted, Is.False);
			Assert.That(rm.TouchCount, Is.EqualTo(2));
			Assert.That(rm.AggregateVersion, Is.EqualTo(3));
		}

		[Test]
		public async Task Verify_framework_internal_exception_fixer_does_not_impede_reading()
		{
			//Simulate generation of an internal exception, 
			SimpleTestAtomicReadModel.TouchMax = 1; //we want at maximum one touch to generate exception
			SimpleTestAtomicReadModel.GenerateInternalExceptionforMaxTouch = true;

			//first step, create two touch events, the second one generates a problem
			Changeset changeset = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

			//And finally check if everything is projected
			await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);

			//we need to wait to understand if it was projected
			GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

			//First readmodel have only one touch
			var evt = changeset.Events[0] as DomainEvent;
			var wrapper = _container.Resolve<IAtomicCollectionWrapper<SimpleTestAtomicReadModel>>();
			var rm = await wrapper.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);

			Assert.That(rm.Faulted);
			Assert.That(rm.FaultRetryCount, Is.EqualTo(0)); //Fault retry was saved in database but it is not immedaitely visible
			Assert.That(rm.TouchCount, Is.EqualTo(1)); //second touch gnerated another exception.
			Assert.That(rm.AggregateVersion, Is.EqualTo(3)); //Created, then 

			rm = await wrapper.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);

			Assert.That(rm.Faulted);
			Assert.That(rm.FaultRetryCount, Is.EqualTo(1), "Increment failure count"); //we have 1 failure retry, previous one.
			Assert.That(rm.TouchCount, Is.EqualTo(1)); //second touch gnerated another exception.
			Assert.That(rm.AggregateVersion, Is.EqualTo(3)); //Created, then 

			for (int i = 0; i < AtomicMongoCollectionWrapper<SimpleTestAtomicReadModel>.MaxNumberOfRetryToReprojectFaultedReadmodels; i++)
			{
				rm = await wrapper.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);
			}

			Assert.That(rm.Faulted);
			Assert.That(rm.FaultRetryCount, Is.EqualTo(AtomicMongoCollectionWrapper<SimpleTestAtomicReadModel>.MaxNumberOfRetryToReprojectFaultedReadmodels), "Too much retry");
		}
	}
}
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
	[TestFixture]
	public class LiveAtomicReadModelProcessorBatchCatchupTests : AtomicProjectionEngineTestBase
	{
		#region Basic Batch Catchup Tests

		[Test]
		public async Task CatchupBatch_with_empty_collection_should_not_throw()
		{
			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
			var emptyList = new List<SimpleTestAtomicReadModel>();

			Assert.DoesNotThrowAsync(() => sut.CatchupBatchAsync(emptyList));
		}

		[Test]
		public void CatchupBatch_with_null_collection_should_throw()
		{
			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();

			Assert.ThrowsAsync<ArgumentNullException>(() =>
				sut.CatchupBatchAsync<SimpleTestAtomicReadModel>(null));
		}

		[Test]
		public async Task CatchupBatch_should_update_single_readmodel()
		{
			// Arrange: Create aggregate with 3 events, then add one more
			var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
			var c2 = await GenerateTouchedEvent().ConfigureAwait(false);

			// Create readmodel at version before last touch event
			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
			var rm = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
				((DomainEvent)c1.Events[0]).AggregateId.AsString(),
				c1.AggregateVersion).ConfigureAwait(false);

			var initialTouchCount = rm.TouchCount;
			Assert.That(rm.AggregateVersion, Is.EqualTo(c1.AggregateVersion));

			// Act: Batch catchup
			await sut.CatchupBatchAsync(new[] { rm }).ConfigureAwait(false);

			// Assert: Readmodel should now be at latest version
			Assert.That(rm.AggregateVersion, Is.EqualTo(c2.AggregateVersion));
			Assert.That(rm.TouchCount, Is.EqualTo(initialTouchCount + 1));
		}

		[Test]
		public async Task CatchupBatch_should_update_multiple_readmodels_at_different_versions()
		{
			// Arrange: Create first aggregate with 4 events
			var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
			var c1_2 = await GenerateTouchedEvent().ConfigureAwait(false);
			var aggregate1Id = ((DomainEvent)c1.Events[0]).AggregateId.AsString();

			// Create second aggregate with 3 events
			_aggregateIdSeed++;
			_aggregateVersion = 1; // Reset version for new aggregate
			var c2 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
			var aggregate2Id = ((DomainEvent)c2.Events[0]).AggregateId.AsString();

			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();

			// Create rm1 at version 2 (missing 2 events)
			var rm1 = await sut.ProcessAsync<SimpleTestAtomicReadModel>(aggregate1Id, 2).ConfigureAwait(false);
			// Create rm2 at version 3 (already at latest)
			var rm2 = await sut.ProcessAsync<SimpleTestAtomicReadModel>(aggregate2Id, 3).ConfigureAwait(false);

			Assert.That(rm1, Is.Not.Null, "rm1 should not be null");
			Assert.That(rm2, Is.Not.Null, "rm2 should not be null");

			var rm1InitialVersion = rm1.AggregateVersion;
			var rm1InitialTouchCount = rm1.TouchCount;
			var rm2InitialVersion = rm2.AggregateVersion;
			var rm2InitialTouchCount = rm2.TouchCount;

			// Act: Batch catchup both
			await sut.CatchupBatchAsync(new[] { rm1, rm2 }).ConfigureAwait(false);

			// Assert: rm1 should be at latest version (4)
			Assert.That(rm1.AggregateVersion, Is.EqualTo(c1_2.AggregateVersion));
			Assert.That(rm1.AggregateVersion, Is.GreaterThan(rm1InitialVersion));
			Assert.That(rm1.TouchCount, Is.GreaterThan(rm1InitialTouchCount));

			// rm2 should remain unchanged (was already at latest)
			Assert.That(rm2.AggregateVersion, Is.EqualTo(rm2InitialVersion));
			Assert.That(rm2.TouchCount, Is.EqualTo(rm2InitialTouchCount));
		}

		[Test]
		public async Task CatchupBatch_should_handle_readmodels_already_up_to_date()
		{
			// Arrange
			var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
			var aggregateId = ((DomainEvent)c1.Events[0]).AggregateId.AsString();

			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
			var rm = await sut.ProcessAsync<SimpleTestAtomicReadModel>(aggregateId, Int32.MaxValue).ConfigureAwait(false);

			var initialVersion = rm.AggregateVersion;
			var initialTouchCount = rm.TouchCount;

			// Act: Batch catchup (should be no-op)
			await sut.CatchupBatchAsync(new[] { rm }).ConfigureAwait(false);

			// Assert: No change
			Assert.That(rm.AggregateVersion, Is.EqualTo(initialVersion));
			Assert.That(rm.TouchCount, Is.EqualTo(initialTouchCount));
		}

		#endregion

		#region Multiple Aggregates Tests

		[Test]
		public async Task CatchupBatch_should_handle_many_aggregates()
		{
			// Arrange: Create 10 aggregates with events
			var readmodels = new List<SimpleTestAtomicReadModel>();
			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();

			for (int i = 0; i < 10; i++)
			{
				_aggregateVersion = 1; // Reset version for new aggregate
				var cs = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
				await GenerateTouchedEvent().ConfigureAwait(false); // Add one more event that won't be in the readmodel

				// Create readmodel at partial version (version 2)
				var aggregateId = ((DomainEvent)cs.Events[0]).AggregateId.AsString();
				var rm = await sut.ProcessAsync<SimpleTestAtomicReadModel>(aggregateId, 2).ConfigureAwait(false);
				Assert.That(rm, Is.Not.Null, $"Readmodel for aggregate {i} should not be null");
				readmodels.Add(rm);

				_aggregateIdSeed++;
			}

			// Verify all readmodels are at version 2
			foreach (var rm in readmodels)
			{
				Assert.That(rm.AggregateVersion, Is.EqualTo(2));
			}

			// Act: Batch catchup
			await sut.CatchupBatchAsync(readmodels).ConfigureAwait(false);

			// Assert: All readmodels should be at version 4
			foreach (var rm in readmodels)
			{
				Assert.That(rm.AggregateVersion, Is.EqualTo(4));
			}
		}

		[Test]
		public async Task CatchupBatch_should_handle_aggregates_at_vastly_different_versions()
		{
			// Arrange: Create aggregates at different stages
			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();

			// Aggregate 1: Create 10 events, readmodel at version 1
			await GenerateCreatedEvent().ConfigureAwait(false);
			for (int i = 0; i < 9; i++)
			{
				await GenerateTouchedEvent().ConfigureAwait(false);
			}
			var aggregate1Id = new SampleAggregateId(_aggregateIdSeed).AsString();
			var rm1 = await sut.ProcessAsync<SimpleTestAtomicReadModel>(aggregate1Id, 1).ConfigureAwait(false);
			Assert.That(rm1, Is.Not.Null, "rm1 should not be null");

			// Aggregate 2: Create 3 events, readmodel at version 2
			_aggregateIdSeed++;
			_aggregateVersion = 1; // Reset version for new aggregate
			await GenerateCreatedEvent().ConfigureAwait(false);
			await GenerateTouchedEvent().ConfigureAwait(false);
			await GenerateTouchedEvent().ConfigureAwait(false);
			var aggregate2Id = new SampleAggregateId(_aggregateIdSeed).AsString();
			var rm2 = await sut.ProcessAsync<SimpleTestAtomicReadModel>(aggregate2Id, 2).ConfigureAwait(false);
			Assert.That(rm2, Is.Not.Null, "rm2 should not be null");

			// Aggregate 3: Create 1 event, readmodel at version 0 (new readmodel)
			_aggregateIdSeed++;
			_aggregateVersion = 1; // Reset version for new aggregate
			await GenerateCreatedEvent().ConfigureAwait(false);
			var aggregate3Id = new SampleAggregateId(_aggregateIdSeed).AsString();
			var rm3 = _container.Resolve<IAtomicReadModelFactory>().Create<SimpleTestAtomicReadModel>(aggregate3Id);

			Assert.That(rm1.AggregateVersion, Is.EqualTo(1));
			Assert.That(rm2.AggregateVersion, Is.EqualTo(2));
			Assert.That(rm3.AggregateVersion, Is.EqualTo(0));

			// Act: Batch catchup all three
			await sut.CatchupBatchAsync(new[] { rm1, rm2, rm3 }).ConfigureAwait(false);

			// Assert: All should be at their respective final versions
			Assert.That(rm1.AggregateVersion, Is.EqualTo(10));
			Assert.That(rm2.AggregateVersion, Is.EqualTo(3));
			Assert.That(rm3.AggregateVersion, Is.EqualTo(1));
		}

		#endregion

		#region Edge Cases Tests

		[Test]
		public async Task CatchupBatch_should_handle_nonexistent_aggregate()
		{
			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();

			// Create a readmodel for a non-existent aggregate
			var rm = new SimpleTestAtomicReadModel("NonExistent_123");

			// Act: Should not throw - just won't find any events
			await sut.CatchupBatchAsync(new[] { rm }).ConfigureAwait(false);

			// Assert: Readmodel stays at version 0
			Assert.That(rm.AggregateVersion, Is.EqualTo(0));
		}

		[Test]
		public async Task CatchupBatch_should_handle_mix_of_existing_and_nonexistent()
		{
			// Arrange: Create one real aggregate
			var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
			var aggregateId = ((DomainEvent)c1.Events[0]).AggregateId.AsString();

			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();

			// Create readmodel at partial version
			var rmReal = await sut.ProcessAsync<SimpleTestAtomicReadModel>(aggregateId, 2).ConfigureAwait(false);
			// Create readmodel for non-existent aggregate
			var rmFake = new SimpleTestAtomicReadModel("NonExistent_456");

			// Act: Batch catchup both
			await sut.CatchupBatchAsync(new[] { rmReal, rmFake }).ConfigureAwait(false);

			// Assert: Real readmodel should be caught up
			Assert.That(rmReal.AggregateVersion, Is.EqualTo(3));
			// Fake readmodel should stay at version 0
			Assert.That(rmFake.AggregateVersion, Is.EqualTo(0));
		}

		[Test]
		public async Task CatchupBatch_with_cancellation_token()
		{
			// Arrange
			var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
			var aggregateId = ((DomainEvent)c1.Events[0]).AggregateId.AsString();

			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
			var rm = await sut.ProcessAsync<SimpleTestAtomicReadModel>(aggregateId, 2).ConfigureAwait(false);

			using var cts = new CancellationTokenSource();
			cts.Cancel();

			// Act & Assert: Should throw OperationCanceledException
			Assert.ThrowsAsync<OperationCanceledException>(() =>
				sut.CatchupBatchAsync(new[] { rm }, cts.Token));
		}

		#endregion

		#region Comparison with Single Catchup Tests

		[Test]
		public async Task CatchupBatch_should_produce_same_result_as_individual_catchups()
		{
			// Arrange: Create multiple aggregates
			var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
			var batchReadmodels = new List<SimpleTestAtomicReadModel>();
			var individualReadmodels = new List<SimpleTestAtomicReadModel>();

			for (int i = 0; i < 5; i++)
			{
				_aggregateVersion = 1; // Reset version for new aggregate
				var cs = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
				await GenerateTouchedEvent().ConfigureAwait(false);
				var aggregateId = ((DomainEvent)cs.Events[0]).AggregateId.AsString();

				// Create two readmodels at version 2 - one for batch, one for individual
				var rmBatch = await sut.ProcessAsync<SimpleTestAtomicReadModel>(aggregateId, 2).ConfigureAwait(false);
				var rmIndividual = await sut.ProcessAsync<SimpleTestAtomicReadModel>(aggregateId, 2).ConfigureAwait(false);

				Assert.That(rmBatch, Is.Not.Null, $"rmBatch at index {i} should not be null");
				Assert.That(rmIndividual, Is.Not.Null, $"rmIndividual at index {i} should not be null");

				batchReadmodels.Add(rmBatch);
				individualReadmodels.Add(rmIndividual);

				_aggregateIdSeed++;
			}

			// Act: Catchup batch
			await sut.CatchupBatchAsync(batchReadmodels).ConfigureAwait(false);

			// Act: Catchup individually
			foreach (var rm in individualReadmodels)
			{
				await sut.CatchupAsync(rm).ConfigureAwait(false);
			}

			// Assert: Both should produce same results
			for (int i = 0; i < 5; i++)
			{
				Assert.That(batchReadmodels[i].AggregateVersion,
					Is.EqualTo(individualReadmodels[i].AggregateVersion),
					$"Version mismatch at index {i}");
				Assert.That(batchReadmodels[i].TouchCount,
					Is.EqualTo(individualReadmodels[i].TouchCount),
					$"TouchCount mismatch at index {i}");
			}
		}

		#endregion
	}
}

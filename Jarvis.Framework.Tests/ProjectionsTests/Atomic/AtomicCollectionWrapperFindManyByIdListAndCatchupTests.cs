using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic;

[TestFixture]
public class AtomicCollectionWrapperFindManyByIdListAndCatchupTests : AtomicCollectionWrapperTestsBase
{
	[SetUp]
	public void SetUp()
	{
		Init();
		InitSingleTest();
	}

	#region Basic Tests

	[Test]
	public async Task FindManyByIdListAndCatchup_should_return_empty_for_empty_list()
	{
		var result = await _sut.FindManyByIdListAndCatchupAsync(Array.Empty<string>()).ConfigureAwait(false);

		Assert.That(result.Count, Is.EqualTo(0));
	}

	[Test]
	public void FindManyByIdListAndCatchup_should_throw_for_null_list()
	{
		Assert.ThrowsAsync<ArgumentNullException>(() =>
			_sut.FindManyByIdListAndCatchupAsync(null));
	}

	[Test]
	public async Task FindManyByIdListAndCatchup_should_return_existing_readmodel_with_catchup()
	{
		// Arrange: Create and persist a readmodel at partial version
		var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var createCs = GenerateCreatedEvent(false);
		rm.ProcessChangeset(createCs);
		await _sut.UpsertAsync(rm).ConfigureAwait(false);

		// Add more events in NStore that the readmodel doesn't have
		GenerateTouchedEvent(false);
		GenerateTouchedEvent(false);
		// Readmodel is at version 1, NStore has events up to version 3

		// Act
		var result = await _sut.FindManyByIdListAndCatchupAsync(new[] { rm.Id }).ConfigureAwait(false);

		// Assert
		Assert.That(result.Count, Is.EqualTo(1));
		var catchedUp = result.First();
		Assert.That(catchedUp.AggregateVersion, Is.EqualTo(3));
		Assert.That(catchedUp.TouchCount, Is.EqualTo(2));
	}

	[Test]
	public async Task FindManyByIdListAndCatchup_should_create_and_project_missing_readmodel()
	{
		// Arrange: Create events in NStore but don't persist readmodel
		var createCs = GenerateCreatedEvent(false);
		GenerateTouchedEvent(false);
		var aggregateId = ((DomainEvent)createCs.Events[0]).AggregateId.AsString();

		// Verify readmodel doesn't exist in storage
		var existing = await _sut.FindOneByIdAsync(aggregateId).ConfigureAwait(false);
		Assert.That(existing, Is.Null);

		// Act
		var result = await _sut.FindManyByIdListAndCatchupAsync(new[] { aggregateId }).ConfigureAwait(false);

		// Assert
		Assert.That(result.Count, Is.EqualTo(1));
		var projected = result.First();
		Assert.That(projected.Id, Is.EqualTo(aggregateId));
		Assert.That(projected.AggregateVersion, Is.EqualTo(2));
		Assert.That(projected.TouchCount, Is.EqualTo(1));
	}

	[Test]
	public async Task FindManyByIdListAndCatchup_should_exclude_ids_with_no_events()
	{
		// Arrange: Create one aggregate with events
		var createCs = GenerateCreatedEvent(false);
		var realAggregateId = ((DomainEvent)createCs.Events[0]).AggregateId.AsString();

		// Non-existent aggregate ID with no events
		var fakeAggregateId = "NonExistent_999999";

		// Act
		var result = await _sut.FindManyByIdListAndCatchupAsync(
			new[] { realAggregateId, fakeAggregateId }).ConfigureAwait(false);

		// Assert: Only the real aggregate should be returned
		Assert.That(result.Count, Is.EqualTo(1));
		Assert.That(result.First().Id, Is.EqualTo(realAggregateId));
	}

	#endregion

	#region Mixed Existing and Missing Readmodels

	[Test]
	public async Task FindManyByIdListAndCatchup_should_handle_mix_of_existing_and_missing()
	{
		// Arrange: Create and persist first readmodel
		var rm1 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var create1 = GenerateCreatedEvent(false);
		rm1.ProcessChangeset(create1);
		await _sut.UpsertAsync(rm1).ConfigureAwait(false);
		GenerateTouchedEvent(false); // Extra event not in persisted readmodel

		// Create second aggregate with events but no persisted readmodel
		_aggregateIdSeed++;
		_aggregateVersion = 1;
		var create2 = GenerateCreatedEvent(false);
		GenerateTouchedEvent(false);
		GenerateTouchedEvent(false);
		var aggregate2Id = ((DomainEvent)create2.Events[0]).AggregateId.AsString();

		// Act
		var result = await _sut.FindManyByIdListAndCatchupAsync(
			new[] { rm1.Id, aggregate2Id }).ConfigureAwait(false);

		// Assert
		Assert.That(result.Count, Is.EqualTo(2));

		var result1 = result.First(r => r.Id == rm1.Id);
		Assert.That(result1.AggregateVersion, Is.EqualTo(2), "Existing readmodel should be caught up");

		var result2 = result.First(r => r.Id == aggregate2Id);
		Assert.That(result2.AggregateVersion, Is.EqualTo(3), "New readmodel should be fully projected");
	}

	[Test]
	public async Task FindManyByIdListAndCatchup_should_handle_multiple_missing_readmodels()
	{
		// Arrange: Create multiple aggregates with events but no persisted readmodels
		var aggregateIds = new List<string>();

		for (int i = 0; i < 5; i++)
		{
			var createCs = GenerateCreatedEvent(false);
			GenerateTouchedEvent(false);
			aggregateIds.Add(((DomainEvent)createCs.Events[0]).AggregateId.AsString());

			_aggregateIdSeed++;
			_aggregateVersion = 1;
		}

		// Act
		var result = await _sut.FindManyByIdListAndCatchupAsync(aggregateIds).ConfigureAwait(false);

		// Assert
		Assert.That(result.Count, Is.EqualTo(5));
		foreach (var rm in result)
		{
			Assert.That(rm.AggregateVersion, Is.EqualTo(2));
			Assert.That(rm.TouchCount, Is.EqualTo(1));
		}
	}

	#endregion

	#region Batch Size Tests

	[Test]
	public async Task FindManyByIdListAndCatchup_should_respect_batch_size()
	{
		// Arrange: Create 15 aggregates with events (mix of persisted and not)
		var aggregateIds = new List<string>();

		for (int i = 0; i < 15; i++)
		{
			var createCs = GenerateCreatedEvent(false);
			GenerateTouchedEvent(false);
			var aggregateId = ((DomainEvent)createCs.Events[0]).AggregateId.AsString();
			aggregateIds.Add(aggregateId);

			// Persist only every other readmodel
			if (i % 2 == 0)
			{
				var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
				rm.ProcessChangeset(createCs);
				await _sut.UpsertAsync(rm).ConfigureAwait(false);
			}

			_aggregateIdSeed++;
			_aggregateVersion = 1;
		}

		// Act: Use small batch size
		var result = await _sut.FindManyByIdListAndCatchupAsync(
			aggregateIds,
			maxBatchSize: 5).ConfigureAwait(false);

		// Assert: All should be caught up
		Assert.That(result.Count, Is.EqualTo(15));
		foreach (var rm in result)
		{
			Assert.That(rm.AggregateVersion, Is.EqualTo(2));
		}
	}

	#endregion

	#region Edge Cases

	[Test]
	public async Task FindManyByIdListAndCatchup_should_handle_duplicate_ids_in_list()
	{
		// Arrange: Create aggregate with events
		var createCs = GenerateCreatedEvent(false);
		GenerateTouchedEvent(false);
		var aggregateId = ((DomainEvent)createCs.Events[0]).AggregateId.AsString();

		// Act: Pass duplicate IDs (this might return duplicate results or dedupe - depends on implementation)
		var result = await _sut.FindManyByIdListAndCatchupAsync(
			new[] { aggregateId, aggregateId }).ConfigureAwait(false);

		// Assert: Should return at least one result
		Assert.That(result.Count, Is.GreaterThanOrEqualTo(1));
		Assert.That(result.All(r => r.Id == aggregateId), Is.True);
	}

	[Test]
	public async Task FindManyByIdListAndCatchup_should_handle_all_nonexistent_ids()
	{
		// Act
		var result = await _sut.FindManyByIdListAndCatchupAsync(
			new[] { "NonExistent_1", "NonExistent_2", "NonExistent_3" }).ConfigureAwait(false);

		// Assert: All IDs have no events, so result should be empty
		Assert.That(result.Count, Is.EqualTo(0));
	}

	[Test]
	public async Task FindManyByIdListAndCatchup_should_handle_readmodel_already_up_to_date()
	{
		// Arrange: Create and persist readmodel at current version
		var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var createCs = GenerateCreatedEvent(false);
		rm.ProcessChangeset(createCs);
		var touchCs = GenerateTouchedEvent(false);
		rm.ProcessChangeset(touchCs);
		await _sut.UpsertAsync(rm).ConfigureAwait(false);

		// Readmodel is at version 2, no additional events in NStore

		// Act
		var result = await _sut.FindManyByIdListAndCatchupAsync(new[] { rm.Id }).ConfigureAwait(false);

		// Assert: Should return readmodel unchanged
		Assert.That(result.Count, Is.EqualTo(1));
		Assert.That(result.First().AggregateVersion, Is.EqualTo(2));
		Assert.That(result.First().TouchCount, Is.EqualTo(1));
	}

	[Test]
	public void FindManyByIdListAndCatchup_should_support_cancellation()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert: Should throw OperationCanceledException
		Assert.ThrowsAsync<OperationCanceledException>(() =>
			_sut.FindManyByIdListAndCatchupAsync(new[] { "SomeId" }, cancellationToken: cts.Token));
	}

	#endregion

	#region Comparison with FindManyAndCatchupAsync

	[Test]
	public async Task FindManyByIdListAndCatchup_should_find_unpersisted_readmodels_unlike_FindManyAndCatchup()
	{
		// Arrange: Create aggregate with events but no persisted readmodel
		var createCs = GenerateCreatedEvent(false);
		GenerateTouchedEvent(false);
		var aggregateId = ((DomainEvent)createCs.Events[0]).AggregateId.AsString();

		// Act: Try FindManyAndCatchupAsync - this should NOT find it (no stored readmodel)
		var filterResult = await _sut.FindManyAndCatchupAsync(
			x => x.Id == aggregateId).ConfigureAwait(false);

		// Act: Try FindManyByIdListAndCatchupAsync - this SHOULD find it (creates missing)
		var idListResult = await _sut.FindManyByIdListAndCatchupAsync(
			new[] { aggregateId }).ConfigureAwait(false);

		// Assert
		Assert.That(filterResult.Count, Is.EqualTo(0), "FindManyAndCatchup should not find unpersisted readmodel");
		Assert.That(idListResult.Count, Is.EqualTo(1), "FindManyByIdListAndCatchup should create and project missing readmodel");
		Assert.That(idListResult.First().AggregateVersion, Is.EqualTo(2));
	}

	#endregion
}

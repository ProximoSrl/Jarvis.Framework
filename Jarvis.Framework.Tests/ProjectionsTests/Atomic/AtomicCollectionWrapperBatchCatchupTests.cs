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
public class AtomicCollectionWrapperBatchCatchupTests : AtomicCollectionWrapperTestsBase
{
	[SetUp]
	public void SetUp()
	{
		Init();
		InitSingleTest();
	}

	#region Basic FindManyAndCatchupAsync Tests

	[Test]
	public async Task FindManyAndCatchup_should_return_empty_when_no_match()
	{
		// Arrange: Create a readmodel with different id
		var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var createCs = GenerateCreatedEvent(false);
		rm.ProcessChangeset(createCs);
		await _sut.UpsertAsync(rm).ConfigureAwait(false);

		// Act: Filter that won't match
		var result = await _sut.FindManyAndCatchupAsync(
			x => x.Id.StartsWith("NonExistent")).ConfigureAwait(false);

		// Assert
		Assert.That(result.Count, Is.EqualTo(0));
	}

	[Test]
	public async Task FindManyAndCatchup_should_catchup_single_readmodel()
	{
		// Arrange: Create and persist readmodel at partial version
		var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var createCs = GenerateCreatedEvent(false);
		rm.ProcessChangeset(createCs);
		await _sut.UpsertAsync(rm).ConfigureAwait(false);

		// Add more events in NStore that the readmodel doesn't have
		var touchCs = GenerateTouchedEvent(false);
		// Readmodel is at version 1, NStore has events up to version 2

		// Act
		var result = await _sut.FindManyAndCatchupAsync(
			x => x.Id == rm.Id).ConfigureAwait(false);

		// Assert
		Assert.That(result.Count, Is.EqualTo(1));
		var catchedUp = result.First();
		Assert.That(catchedUp.AggregateVersion, Is.EqualTo(2));
		Assert.That(catchedUp.TouchCount, Is.EqualTo(1));
	}

	[Test]
	public async Task FindManyAndCatchup_should_catchup_multiple_readmodels()
	{
		// Arrange: Create multiple readmodels
		var readmodelIds = new List<string>();

		for (int i = 0; i < 5; i++)
		{
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			var createCs = GenerateCreatedEvent(false);
			rm.ProcessChangeset(createCs);
			await _sut.UpsertAsync(rm).ConfigureAwait(false);

			// Add more events in NStore
			GenerateTouchedEvent(false);
			GenerateTouchedEvent(false);

			readmodelIds.Add(rm.Id);

			_aggregateIdSeed++;
			_aggregateVersion = 1;
		}

		// All readmodels are at version 1, NStore has events up to version 3 for each

		// Act
		var result = await _sut.FindManyAndCatchupAsync(
			x => readmodelIds.Contains(x.Id)).ConfigureAwait(false);

		// Assert
		Assert.That(result.Count, Is.EqualTo(5));
		foreach (var rm in result)
		{
			Assert.That(rm.AggregateVersion, Is.EqualTo(3), $"Readmodel {rm.Id} should be at version 3");
			Assert.That(rm.TouchCount, Is.EqualTo(2), $"Readmodel {rm.Id} should have 2 touches");
		}
	}

	[Test]
	public async Task FindManyAndCatchup_should_handle_readmodels_at_different_versions()
	{
		// Arrange: Create readmodels at different versions
		var readmodelIds = new List<string>();

		// Readmodel 1: version 1 (missing 2 events)
		var rm1 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var create1 = GenerateCreatedEvent(false);
		rm1.ProcessChangeset(create1);
		await _sut.UpsertAsync(rm1).ConfigureAwait(false);
		GenerateTouchedEvent(false);
		GenerateTouchedEvent(false);
		readmodelIds.Add(rm1.Id);

		// Readmodel 2: version 2 (missing 1 event)
		_aggregateIdSeed++;
		_aggregateVersion = 1;
		var rm2 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var create2 = GenerateCreatedEvent(false);
		rm2.ProcessChangeset(create2);
		var touch2 = GenerateTouchedEvent(false);
		rm2.ProcessChangeset(touch2);
		await _sut.UpsertAsync(rm2).ConfigureAwait(false);
		GenerateTouchedEvent(false);
		readmodelIds.Add(rm2.Id);

		// Readmodel 3: version 3 (already up to date)
		_aggregateIdSeed++;
		_aggregateVersion = 1;
		var rm3 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var create3 = GenerateCreatedEvent(false);
		rm3.ProcessChangeset(create3);
		var touch3a = GenerateTouchedEvent(false);
		rm3.ProcessChangeset(touch3a);
		var touch3b = GenerateTouchedEvent(false);
		rm3.ProcessChangeset(touch3b);
		await _sut.UpsertAsync(rm3).ConfigureAwait(false);
		readmodelIds.Add(rm3.Id);

		// Act
		var result = await _sut.FindManyAndCatchupAsync(
			x => readmodelIds.Contains(x.Id)).ConfigureAwait(false);

		// Assert
		Assert.That(result.Count, Is.EqualTo(3));

		var result1 = result.First(r => r.Id == rm1.Id);
		Assert.That(result1.AggregateVersion, Is.EqualTo(3), "rm1 should be caught up to version 3");

		var result2 = result.First(r => r.Id == rm2.Id);
		Assert.That(result2.AggregateVersion, Is.EqualTo(3), "rm2 should be caught up to version 3");

		var result3 = result.First(r => r.Id == rm3.Id);
		Assert.That(result3.AggregateVersion, Is.EqualTo(3), "rm3 should remain at version 3");
	}

	#endregion

	#region Batch Size Tests

	[Test]
	public async Task FindManyAndCatchup_should_respect_batch_size()
	{
		// Arrange: Create 15 readmodels
		var readmodelIds = new List<string>();

		for (int i = 0; i < 15; i++)
		{
			var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
			var createCs = GenerateCreatedEvent(false);
			rm.ProcessChangeset(createCs);
			await _sut.UpsertAsync(rm).ConfigureAwait(false);

			// Add event in NStore
			GenerateTouchedEvent(false);

			readmodelIds.Add(rm.Id);

			_aggregateIdSeed++;
			_aggregateVersion = 1;
		}

		// Act: Use batch size of 5
		var result = await _sut.FindManyAndCatchupAsync(
			x => readmodelIds.Contains(x.Id),
			maxBatchSize: 5).ConfigureAwait(false);

		// Assert: All should be caught up
		Assert.That(result.Count, Is.EqualTo(15));
		foreach (var rm in result)
		{
			Assert.That(rm.AggregateVersion, Is.EqualTo(2), $"Readmodel {rm.Id} should be at version 2");
		}
	}

	#endregion

	#region Edge Cases

	[Test]
	public async Task FindManyAndCatchup_should_handle_no_new_events()
	{
		// Arrange: Create readmodel already at latest version
		var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var createCs = GenerateCreatedEvent(false);
		rm.ProcessChangeset(createCs);
		var touchCs = GenerateTouchedEvent(false);
		rm.ProcessChangeset(touchCs);
		await _sut.UpsertAsync(rm).ConfigureAwait(false);

		// No additional events in NStore

		// Act
		var result = await _sut.FindManyAndCatchupAsync(
			x => x.Id == rm.Id).ConfigureAwait(false);

		// Assert: Should return readmodel unchanged
		Assert.That(result.Count, Is.EqualTo(1));
		Assert.That(result.First().AggregateVersion, Is.EqualTo(2));
		Assert.That(result.First().TouchCount, Is.EqualTo(1));
	}

	[Test]
	public async Task FindManyAndCatchup_with_filter_by_property()
	{
		// Arrange: Create readmodels with different states
		var rm1 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var create1 = GenerateCreatedEvent(false);
		rm1.ProcessChangeset(create1);
		await _sut.UpsertAsync(rm1).ConfigureAwait(false);
		GenerateTouchedEvent(false);

		_aggregateIdSeed++;
		_aggregateVersion = 1;
		var rm2 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
		var create2 = GenerateCreatedEvent(false);
		rm2.ProcessChangeset(create2);
		var touch2 = GenerateTouchedEvent(false);
		rm2.ProcessChangeset(touch2);
		await _sut.UpsertAsync(rm2).ConfigureAwait(false);
		GenerateTouchedEvent(false);

		// Act: Filter by TouchCount
		var result = await _sut.FindManyAndCatchupAsync(
			x => x.TouchCount == 0).ConfigureAwait(false);

		// Assert: Only rm1 should match (has 0 touches in stored version)
		Assert.That(result.Count, Is.EqualTo(1));
		Assert.That(result.First().Id, Is.EqualTo(rm1.Id));
		// After catchup, it should have 1 touch
		Assert.That(result.First().TouchCount, Is.EqualTo(1));
	}

	[Test]
	public void FindManyAndCatchup_should_support_cancellation()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert: Should throw OperationCanceledException
		Assert.ThrowsAsync<OperationCanceledException>(() =>
			_sut.FindManyAndCatchupAsync(x => true, cancellationToken: cts.Token));
	}

	#endregion
}

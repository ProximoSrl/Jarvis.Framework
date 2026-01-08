using Castle.Core.Logging;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicCollectionWrapperUpdateVersionBatchTests : AtomicCollectionWrapperTestsBase
    {
        [SetUp]
        public void SetUp()
        {
            Init();
            InitSingleTest();
        }

        #region Basic UpdateVersionBatch Tests

        [Test]
        public async Task UpdateVersionBatch_should_update_version_fields_for_existing_readmodels()
        {
            // Arrange: Create and persist readmodels
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 3; i++)
            {
                _aggregateIdSeed = 100 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                await _sut.UpsertAsync(rm).ConfigureAwait(false);
                readmodels.Add(rm);
            }

            // Modify version fields in memory (simulating processing additional events that don't modify the readmodel)
            var updatedReadmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < readmodels.Count; i++)
            {
                _aggregateIdSeed = 100 + i;
                // Generate an event that doesn't modify the readmodel
                var notHandledEvent = GenerateSampleAggregateNotHandledEvent(false);
                readmodels[i].ProcessChangeset(notHandledEvent);
                updatedReadmodels.Add(readmodels[i]);
            }

            // Act: Update versions in batch
            await _sut.UpdateVersionBatchAsync(updatedReadmodels).ConfigureAwait(false);

            // Assert: Verify version fields were updated but other fields unchanged
            foreach (var rm in updatedReadmodels)
            {
                var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion), "AggregateVersion should be updated");
                Assert.That(reloaded.ProjectedPosition, Is.EqualTo(rm.ProjectedPosition), "ProjectedPosition should be updated");
                Assert.That(reloaded.TouchCount, Is.EqualTo(0), "TouchCount should remain unchanged (no touch events)");
            }
        }

        [Test]
        public async Task UpdateVersionBatch_should_insert_missing_readmodels()
        {
            // Arrange: Create readmodels that don't exist in DB
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 3; i++)
            {
                _aggregateIdSeed = 200 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                readmodels.Add(rm);
            }

            // Act: Update version batch (should insert since they don't exist)
            await _sut.UpdateVersionBatchAsync(readmodels).ConfigureAwait(false);

            // Assert: Verify all were inserted with full data
            foreach (var rm in readmodels)
            {
                var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null, $"Readmodel {rm.Id} should be inserted");
                Assert.That(reloaded.Id, Is.EqualTo(rm.Id));
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion));
                Assert.That(reloaded.ProjectedPosition, Is.EqualTo(rm.ProjectedPosition));
            }
        }

        [Test]
        public async Task UpdateVersionBatch_should_handle_mixed_existing_and_missing()
        {
            // Arrange: Create one existing readmodel
            _aggregateIdSeed = 300;
            _aggregateVersion = 1;
            var existingRm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            existingRm.ProcessChangeset(changeset);
            await _sut.UpsertAsync(existingRm).ConfigureAwait(false);

            // Prepare batch with mixed existing and new
            var batchReadmodels = new List<SimpleTestAtomicReadModel>();

            // Update version of existing
            var notHandledEvent = GenerateSampleAggregateNotHandledEvent(false);
            existingRm.ProcessChangeset(notHandledEvent);
            batchReadmodels.Add(existingRm);

            // Add new readmodels
            for (int i = 1; i <= 2; i++)
            {
                _aggregateIdSeed = 300 + i;
                _aggregateVersion = 1;
                var newRm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var newChangeset = GenerateCreatedEvent(false);
                newRm.ProcessChangeset(newChangeset);
                batchReadmodels.Add(newRm);
            }

            // Act: Batch update version
            await _sut.UpdateVersionBatchAsync(batchReadmodels).ConfigureAwait(false);

            // Assert: Verify existing was updated (version only)
            var reloadedExisting = await _sut.FindOneByIdAsync(existingRm.Id).ConfigureAwait(false);
            Assert.That(reloadedExisting, Is.Not.Null);
            Assert.That(reloadedExisting.AggregateVersion, Is.EqualTo(2), "Existing readmodel should have version updated");
            Assert.That(reloadedExisting.TouchCount, Is.EqualTo(0), "TouchCount should remain 0");

            // Verify new ones were inserted
            for (int i = 1; i < batchReadmodels.Count; i++)
            {
                var reloaded = await _sut.FindOneByIdAsync(batchReadmodels[i].Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null, $"New readmodel {batchReadmodels[i].Id} should be inserted");
                Assert.That(reloaded.Id, Is.EqualTo(batchReadmodels[i].Id));
            }
        }

        [Test]
        public async Task UpdateVersionBatch_should_update_only_version_fields_not_other_properties()
        {
            // Arrange: Create and persist a readmodel with TouchCount = 1
            _aggregateIdSeed = 400;
            _aggregateVersion = 1;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            var touchEvent = GenerateTouchedEvent(false);
            rm.ProcessChangeset(touchEvent);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            // Manually alter TouchCount on database to a different value
            var filter = Builders<SimpleTestAtomicReadModel>.Filter.Eq("_id", rm.Id);
            var update = Builders<SimpleTestAtomicReadModel>.Update.Set(m => m.TouchCount, 999);
            await _collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            // Verify the manual change
            var beforeUpdate = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(beforeUpdate.TouchCount, Is.EqualTo(999));

            // Process another non-modifying event to update version
            var notHandledEvent = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(notHandledEvent);

            // Act: Update version batch
            await _sut.UpdateVersionBatchAsync(new[] { rm }).ConfigureAwait(false);

            // Assert: Version fields updated, TouchCount unchanged
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion), "AggregateVersion should be updated");
            Assert.That(reloaded.ProjectedPosition, Is.EqualTo(rm.ProjectedPosition), "ProjectedPosition should be updated");
            Assert.That(reloaded.TouchCount, Is.EqualTo(999), "TouchCount should remain unchanged (partial update)");
        }

        #endregion

        #region Edge Cases and Validation Tests

        [Test]
        public async Task UpdateVersionBatch_should_handle_empty_collection_gracefully()
        {
            // Arrange: Empty collection
            var emptyList = new List<SimpleTestAtomicReadModel>();

            // Act & Assert: Should not throw
            Assert.DoesNotThrowAsync(() => _sut.UpdateVersionBatchAsync(emptyList));
        }

        [Test]
        public void UpdateVersionBatch_should_throw_on_null_collection()
        {
            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(() => _sut.UpdateVersionBatchAsync(null));
            Assert.That(ex.ParamName, Is.EqualTo("models"));
        }

        [Test]
        public void UpdateVersionBatch_should_throw_when_model_has_empty_id()
        {
            // Arrange: Create readmodel with empty Id
            _aggregateIdSeed = 500;
            _aggregateVersion = 1;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);

            // Force empty id
            rm.SetPropertyValue(_ => _.Id, string.Empty);

            // Act & Assert
            var ex = Assert.ThrowsAsync<CollectionWrapperException>(() => _sut.UpdateVersionBatchAsync(new[] { rm }));
            Assert.That(ex.Message, Does.Contain("Id property not initialized"));
        }

        [Test]
        public void UpdateVersionBatch_should_throw_on_duplicate_ids()
        {
            // Arrange: Create readmodels with duplicate IDs
            _aggregateIdSeed = 600;
            _aggregateVersion = 1;

            var rm1 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset1 = GenerateCreatedEvent(false);
            rm1.ProcessChangeset(changeset1);

            var rm2 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed)); // Same ID
            rm2.ProcessChangeset(changeset1);

            var batch = new List<SimpleTestAtomicReadModel> { rm1, rm2 };

            // Act & Assert
            var ex = Assert.ThrowsAsync<CollectionWrapperException>(() => _sut.UpdateVersionBatchAsync(batch));
            Assert.That(ex.Message, Does.Contain("Duplicate readmodel Ids found in batch"));
            Assert.That(ex.Message, Does.Contain(rm1.Id));
        }

        [Test]
        public async Task UpdateVersionBatch_should_skip_readmodels_with_ModifiedWithExtraStreamEvents()
        {
            // Arrange: Create readmodels, one with ModifiedWithExtraStreamEvents flag
            var readmodels = new List<SimpleTestAtomicReadModel>();

            for (int i = 0; i < 3; i++)
            {
                _aggregateIdSeed = 700 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);

                if (i == 1)
                {
                    // Mark middle one as modified with extra stream events
                    rm.SetPropertyValue(_ => _.ModifiedWithExtraStreamEvents, true);
                }

                readmodels.Add(rm);
            }

            // Act: Batch update version
            await _sut.UpdateVersionBatchAsync(readmodels).ConfigureAwait(false);

            // Assert: Verify first and third were saved, second was not
            var reloaded0 = await _sut.FindOneByIdAsync(readmodels[0].Id).ConfigureAwait(false);
            var reloaded1 = await _sut.FindOneByIdAsync(readmodels[1].Id).ConfigureAwait(false);
            var reloaded2 = await _sut.FindOneByIdAsync(readmodels[2].Id).ConfigureAwait(false);

            Assert.That(reloaded0, Is.Not.Null, "First readmodel should be inserted");
            Assert.That(reloaded1, Is.Null, "Second readmodel with ModifiedWithExtraStreamEvents should NOT be inserted");
            Assert.That(reloaded2, Is.Not.Null, "Third readmodel should be inserted");
        }

        [Test]
        public async Task UpdateVersionBatch_should_handle_all_readmodels_filtered_out()
        {
            // Arrange: Create readmodels all marked as ModifiedWithExtraStreamEvents
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 3; i++)
            {
                _aggregateIdSeed = 800 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                rm.SetPropertyValue(_ => _.ModifiedWithExtraStreamEvents, true);
                readmodels.Add(rm);
            }

            // Act: Should not throw
            Assert.DoesNotThrowAsync(() => _sut.UpdateVersionBatchAsync(readmodels));

            // Assert: Nothing should be saved
            foreach (var rm in readmodels)
            {
                var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Null);
            }
        }

        #endregion

        #region Performance and Bulk Operation Tests

        [Test]
        public async Task UpdateVersionBatch_should_efficiently_handle_large_batch()
        {
            // Arrange: Create and persist 50 readmodels
            var readmodels = new List<SimpleTestAtomicReadModel>();
            
            for (int i = 0; i < 50; i++)
            {
                _aggregateIdSeed = 900 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                await _sut.UpsertAsync(rm).ConfigureAwait(false);
                readmodels.Add(rm);
            }

            // Update version info - each readmodel processes one more event
            for (int i = 0; i < readmodels.Count; i++)
            {
                _aggregateIdSeed = 900 + i;
                // Each readmodel was at aggregate version 1, now we add one more event
                _aggregateVersion = 2; // Next event will be version 2
                var notHandledEvent = GenerateSampleAggregateNotHandledEvent(false);
                readmodels[i].ProcessChangeset(notHandledEvent);
            }

            // Act: Batch update version
            var startTime = DateTime.UtcNow;
            await _sut.UpdateVersionBatchAsync(readmodels).ConfigureAwait(false);
            var duration = DateTime.UtcNow - startTime;

            // Assert: Verify operation was reasonably fast
            Assert.That(duration.TotalSeconds, Is.LessThan(10), "Batch operation should complete in reasonable time");

            // Verify a sample of readmodels
            var sampleIndices = new[] { 0, 10, 25, 40, 49 };
            foreach (var idx in sampleIndices)
            {
                var reloaded = await _sut.FindOneByIdAsync(readmodels[idx].Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null, $"Readmodel at index {idx} should exist");
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(2), $"Readmodel at index {idx} should have updated version");
            }
        }

        [Test]
        public async Task UpdateVersionBatch_should_handle_concurrent_inserts_gracefully()
        {
            // Arrange: Create readmodels that don't exist yet
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 5; i++)
            {
                _aggregateIdSeed = 1000 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                readmodels.Add(rm);
            }

            // Act: Perform concurrent batch update versions (both will try to insert)
            var task1 = _sut.UpdateVersionBatchAsync(readmodels);
            var task2 = _sut.UpdateVersionBatchAsync(readmodels);

            // Should not throw - duplicate key errors from concurrent inserts should be handled
            await Task.WhenAll(task1, task2).ConfigureAwait(false);

            // Assert: All readmodels should exist
            foreach (var rm in readmodels)
            {
                var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null, $"Readmodel {rm.Id} should exist after concurrent inserts");
                Assert.That(reloaded.Id, Is.EqualTo(rm.Id));
            }
        }

        [Test]
        public async Task UpdateVersionBatch_with_single_readmodel_should_work()
        {
            // Arrange: Single readmodel
            _aggregateIdSeed = 1100;
            _aggregateVersion = 1;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);

            // Act: Update version batch with single item
            await _sut.UpdateVersionBatchAsync(new[] { rm }).ConfigureAwait(false);

            // Assert
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.Id, Is.EqualTo(rm.Id));
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion));
        }

        #endregion

        #region Comparison with UpdateVersionAsync behavior

        [Test]
        public async Task UpdateVersionBatch_should_match_single_UpdateVersionAsync_behavior()
        {
            // Arrange: Create readmodels - half existing, half new
            var existingReadmodels = new List<SimpleTestAtomicReadModel>();
            var newReadmodels = new List<SimpleTestAtomicReadModel>();

            // Create existing
            for (int i = 0; i < 2; i++)
            {
                _aggregateIdSeed = 1200 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                await _sut.UpsertAsync(rm).ConfigureAwait(false);
                existingReadmodels.Add(rm);
            }

            // Create new (not persisted)
            for (int i = 0; i < 2; i++)
            {
                _aggregateIdSeed = 1300 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                newReadmodels.Add(rm);
            }

            // Update versions - existing readmodels process one more event
            for (int i = 0; i < existingReadmodels.Count; i++)
            {
                _aggregateIdSeed = 1200 + i;
                // Each existing readmodel was at version 1, now we add event at version 2
                _aggregateVersion = 2;
                var notHandledEvent = GenerateSampleAggregateNotHandledEvent(false);
                existingReadmodels[i].ProcessChangeset(notHandledEvent);
            }

            // Act: Use batch for all
            var allReadmodels = existingReadmodels.Concat(newReadmodels).ToList();
            await _sut.UpdateVersionBatchAsync(allReadmodels).ConfigureAwait(false);

            // Assert: Verify existing were updated (version only) and new were inserted (full)
            foreach (var rm in existingReadmodels)
            {
                var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(2), "Existing readmodel should have version updated");
            }

            foreach (var rm in newReadmodels)
            {
                var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null, "New readmodel should be inserted");
                Assert.That(reloaded.Id, Is.EqualTo(rm.Id));
            }
        }

        #endregion
    }
}

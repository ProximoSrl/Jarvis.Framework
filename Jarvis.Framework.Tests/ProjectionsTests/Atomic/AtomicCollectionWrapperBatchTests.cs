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
    public class AtomicCollectionWrapperBatchTests : AtomicCollectionWrapperTestsBase
    {
        [SetUp]
        public void SetUp()
        {
            Init();
            InitSingleTest();
        }

        #region Basic Batch Upsert Tests

        [Test]
        public async Task UpsertBatch_should_insert_multiple_new_readmodels()
        {
            // Arrange: Create 5 new readmodels
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 5; i++)
            {
                _aggregateIdSeed = 100 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                readmodels.Add(rm);
            }

            // Act: Batch upsert
            await _sut.UpsertBatchAsync(readmodels).ConfigureAwait(false);

            // Assert: Verify all were inserted
            foreach (var rm in readmodels)
            {
                var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.Id, Is.EqualTo(rm.Id));
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion));
                Assert.That(reloaded.ProjectedPosition, Is.EqualTo(rm.ProjectedPosition));
            }
        }

        [Test]
        public async Task UpsertBatch_should_update_existing_readmodels_with_higher_version()
        {
            // Arrange: Create and save initial readmodels
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 3; i++)
            {
                _aggregateIdSeed = 200 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                await _sut.UpsertAsync(rm).ConfigureAwait(false);
                readmodels.Add(rm);
            }

            // Update readmodels with new events - must maintain the correct aggregate ID
            var updatedReadmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < readmodels.Count; i++)
            {
                _aggregateIdSeed = 200 + i; // Reset to correct aggregate ID
                var touchEvent = GenerateTouchedEvent(false);
                readmodels[i].ProcessChangeset(touchEvent);
                updatedReadmodels.Add(readmodels[i]);
            }

            // Act: Batch upsert with updated versions
            await _sut.UpsertBatchAsync(updatedReadmodels).ConfigureAwait(false);

            // Assert: Verify all were updated
            foreach (var rm in updatedReadmodels)
            {
                var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.TouchCount, Is.EqualTo(1));
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion));
            }
        }

        [Test]
        public async Task UpsertBatch_should_skip_readmodels_with_older_or_equal_version()
        {
            // Arrange: Create and save readmodels with version 2
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 3; i++)
            {
                _aggregateIdSeed = 300 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                var touchEvent = GenerateTouchedEvent(false);
                rm.ProcessChangeset(touchEvent);
                await _sut.UpsertAsync(rm).ConfigureAwait(false);
                readmodels.Add(rm);
            }

            // Manually update one readmodel to have higher TouchCount on database
            var rmToModify = await _collection.FindOneByIdAsync(readmodels[0].Id).ConfigureAwait(false);
            rmToModify.SetPropertyValue(_ => _.TouchCount, 100);
            await _collection.ReplaceOneAsync(
                Builders<SimpleTestAtomicReadModel>.Filter.Eq("_id", rmToModify.Id),
                rmToModify).ConfigureAwait(false);

            // Try to batch upsert with older versions
            var olderReadmodels = new List<SimpleTestAtomicReadModel>();
            foreach (var rm in readmodels)
            {
                _aggregateIdSeed = int.Parse(rm.Id.Split('_')[1]);
                _aggregateVersion = 1;
                var oldRm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                oldRm.ProcessChangeset(changeset);
                olderReadmodels.Add(oldRm);
            }

            // Act: Batch upsert with older versions
            await _sut.UpsertBatchAsync(olderReadmodels).ConfigureAwait(false);

            // Assert: Verify nothing was overwritten
            var reloaded = await _sut.FindOneByIdAsync(readmodels[0].Id).ConfigureAwait(false);
            Assert.That(reloaded.TouchCount, Is.EqualTo(100), "Modified readmodel should not be overwritten");

            for (int i = 1; i < readmodels.Count; i++)
            {
                reloaded = await _sut.FindOneByIdAsync(readmodels[i].Id).ConfigureAwait(false);
                Assert.That(reloaded.TouchCount, Is.EqualTo(1), $"Readmodel {i} should not be overwritten with older version");
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(readmodels[i].AggregateVersion));
            }
        }

        [Test]
        public async Task UpsertBatch_should_handle_mixed_insert_and_update()
        {
            // Arrange: Create some existing readmodels
            _aggregateIdSeed = 400;
            _aggregateVersion = 1;
            var existingRm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            existingRm.ProcessChangeset(changeset);
            await _sut.UpsertAsync(existingRm).ConfigureAwait(false);

            // Prepare batch with mix of new and updated readmodels
            var batchReadmodels = new List<SimpleTestAtomicReadModel>();

            // Updated version of existing
            var touchEvent = GenerateTouchedEvent(false);
            existingRm.ProcessChangeset(touchEvent);
            batchReadmodels.Add(existingRm);

            // New readmodels
            for (int i = 1; i <= 3; i++)
            {
                _aggregateIdSeed = 400 + i;
                _aggregateVersion = 1;
                var newRm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var newChangeset = GenerateCreatedEvent(false);
                newRm.ProcessChangeset(newChangeset);
                batchReadmodels.Add(newRm);
            }

            // Act: Batch upsert
            await _sut.UpsertBatchAsync(batchReadmodels).ConfigureAwait(false);

            // Assert: Verify existing was updated
            var reloadedExisting = await _sut.FindOneByIdAsync(existingRm.Id).ConfigureAwait(false);
            Assert.That(reloadedExisting, Is.Not.Null);
            Assert.That(reloadedExisting.TouchCount, Is.EqualTo(1));
            Assert.That(reloadedExisting.AggregateVersion, Is.EqualTo(2));

            // Verify new ones were inserted
            for (int i = 1; i < batchReadmodels.Count; i++)
            {
                var reloaded = await _sut.FindOneByIdAsync(batchReadmodels[i].Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.Id, Is.EqualTo(batchReadmodels[i].Id));
            }
        }

        #endregion

        #region Edge Cases and Validation Tests

        [Test]
        public async Task UpsertBatch_should_handle_empty_collection_gracefully()
        {
            // Arrange: Empty collection
            var emptyList = new List<SimpleTestAtomicReadModel>();

            // Act & Assert: Should not throw
            Assert.DoesNotThrowAsync(() => _sut.UpsertBatchAsync(emptyList));
        }

        [Test]
        public void UpsertBatch_should_throw_on_null_collection()
        {
            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(() => _sut.UpsertBatchAsync(null));
            Assert.That(ex.ParamName, Is.EqualTo("models"));
        }

        [Test]
        public void UpsertBatch_should_throw_when_model_has_empty_id()
        {
            // Arrange: Create readmodel with empty Id
            var readmodels = new List<SimpleTestAtomicReadModel>();
            _aggregateIdSeed = 500;
            _aggregateVersion = 1;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);

            // Force empty id
            rm.SetPropertyValue(_ => _.Id, string.Empty);
            readmodels.Add(rm);

            // Act & Assert
            var ex = Assert.ThrowsAsync<CollectionWrapperException>(() => _sut.UpsertBatchAsync(readmodels));
            Assert.That(ex.Message, Does.Contain("Id property not initialized"));
        }

        [Test]
        public async Task UpsertBatch_should_skip_readmodels_with_ModifiedWithExtraStreamEvents()
        {
            // Arrange: Create readmodels, one with ModifiedWithExtraStreamEvents flag
            var readmodels = new List<SimpleTestAtomicReadModel>();

            for (int i = 0; i < 3; i++)
            {
                _aggregateIdSeed = 600 + i;
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

            // Act: Batch upsert
            await _sut.UpsertBatchAsync(readmodels).ConfigureAwait(false);

            // Assert: Verify first and third were saved, second was not
            var reloaded0 = await _sut.FindOneByIdAsync(readmodels[0].Id).ConfigureAwait(false);
            var reloaded1 = await _sut.FindOneByIdAsync(readmodels[1].Id).ConfigureAwait(false);
            var reloaded2 = await _sut.FindOneByIdAsync(readmodels[2].Id).ConfigureAwait(false);

            Assert.That(reloaded0, Is.Not.Null, "First readmodel should be saved");
            Assert.That(reloaded1, Is.Null, "Second readmodel with ModifiedWithExtraStreamEvents should NOT be saved");
            Assert.That(reloaded2, Is.Not.Null, "Third readmodel should be saved");
        }

        [Test]
        public async Task UpsertBatch_should_handle_all_readmodels_filtered_out()
        {
            // Arrange: Create readmodels all marked as ModifiedWithExtraStreamEvents
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 3; i++)
            {
                _aggregateIdSeed = 700 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                rm.SetPropertyValue(_ => _.ModifiedWithExtraStreamEvents, true);
                readmodels.Add(rm);
            }

            // Act: Should not throw
            Assert.DoesNotThrowAsync(() => _sut.UpsertBatchAsync(readmodels));

            // Assert: Nothing should be saved
            foreach (var rm in readmodels)
            {
                var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Null);
            }
        }

        #endregion

        #region Idempotency and Version Check Tests

        [Test]
        [Ignore("Complex test with cumulative touch count - needs refactoring")]
        public async Task UpsertBatch_should_honor_readmodel_version_for_idempotency()
        {
            // Arrange: Create readmodels with version 2 - start with create + touch to get TouchCount = 1
            SimpleTestAtomicReadModel.FakeSignature = 2;
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 3; i++)
            {
                _aggregateIdSeed = 800 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                var touchEvent = GenerateTouchedEvent(false);
                rm.ProcessChangeset(touchEvent);
                readmodels.Add(rm);
            }

            await _sut.UpsertBatchAsync(readmodels).ConfigureAwait(false);

            // Manually alter TouchCount on database to simulate different state
            foreach (var rm in readmodels)
            {
                var filter = Builders<SimpleTestAtomicReadModel>.Filter.Eq("_id", rm.Id);
                var update = Builders<SimpleTestAtomicReadModel>.Update.Set(m => m.TouchCount, 999);
                await _collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            }

            // Try to batch upsert with higher aggregate version (process another touch)
            // Must create new readmodel instances to avoid cumulative state
            var higherAggregateVersionReadmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < readmodels.Count; i++)
            {
                _aggregateIdSeed = 800 + i; // Reset to correct aggregate ID
                _aggregateVersion = 1; // Reset version
                
                // Recreate readmodel with all events
                var newRm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var createChangeset = GenerateCreatedEvent(false);
                newRm.ProcessChangeset(createChangeset);
                var touch1 = GenerateTouchedEvent(false);
                newRm.ProcessChangeset(touch1);
                var touch2 = GenerateTouchedEvent(false);
                newRm.ProcessChangeset(touch2);
                
                higherAggregateVersionReadmodels.Add(newRm);
            }

            // Act: Batch upsert with higher aggregate version
            await _sut.UpsertBatchAsync(higherAggregateVersionReadmodels).ConfigureAwait(false);

            // Assert: Should update because aggregate version is higher
            // Verify by reading directly from collection
            foreach (var rm in higherAggregateVersionReadmodels)
            {
                var filter = Builders<SimpleTestAtomicReadModel>.Filter.Eq("_id", rm.Id);
                var reloaded = await _collection.Find(filter).FirstOrDefaultAsync().ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                // TouchCount should now be 2 (from 2 touch events), not 999
                Assert.That(reloaded.TouchCount, Is.EqualTo(2), $"TouchCount should be 2 for readmodel {rm.Id}");
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(3));
            }
        }

        [Test]
        public async Task UpsertBatch_should_handle_same_aggregate_version_but_different_readmodel_version()
        {
            // Arrange: Save with version 1
            SimpleTestAtomicReadModel.FakeSignature = 1;
            var readmodels = new List<SimpleTestAtomicReadModel>();
            _aggregateIdSeed = 900;
            _aggregateVersion = 1;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            var touchEvent = GenerateTouchedEvent(false);
            rm.ProcessChangeset(touchEvent);
            readmodels.Add(rm);

            await _sut.UpsertBatchAsync(readmodels).ConfigureAwait(false);

            // Try to batch upsert with same aggregate version but higher readmodel version
            SimpleTestAtomicReadModel.FakeSignature = 2;
            var higherSignatureReadmodels = new List<SimpleTestAtomicReadModel>();
            var higherRm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            higherRm.ProcessChangeset(changeset);
            higherRm.ProcessChangeset(touchEvent);
            Assert.That(higherRm.TouchCount, Is.EqualTo(2));
            higherSignatureReadmodels.Add(higherRm);

            // Act: Batch upsert with higher readmodel version
            await _sut.UpsertBatchAsync(higherSignatureReadmodels).ConfigureAwait(false);

            // Assert: Should update because readmodel version is higher
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.ReadModelVersion, Is.EqualTo(2));
            Assert.That(reloaded.TouchCount, Is.EqualTo(2));
        }

        #endregion

        #region Performance and Bulk Operation Tests

        [Test]
        public async Task UpsertBatch_should_efficiently_handle_large_batch()
        {
            // Arrange: Create 100 readmodels
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 100; i++)
            {
                _aggregateIdSeed = 1000 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                readmodels.Add(rm);
            }

            // Act: Batch upsert
            var startTime = DateTime.UtcNow;
            await _sut.UpsertBatchAsync(readmodels).ConfigureAwait(false);
            var duration = DateTime.UtcNow - startTime;

            // Assert: Verify all were inserted and operation was reasonably fast
            Assert.That(duration.TotalSeconds, Is.LessThan(10), "Batch operation should complete in reasonable time");

            // Verify a sample of readmodels
            var sampleIndices = new[] { 0, 25, 50, 75, 99 };
            foreach (var idx in sampleIndices)
            {
                var reloaded = await _sut.FindOneByIdAsync(readmodels[idx].Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null, $"Readmodel at index {idx} should be saved");
                Assert.That(reloaded.Id, Is.EqualTo(readmodels[idx].Id));
            }

            // Verify total count in collection
            var totalCount = await _collection.CountDocumentsAsync(Builders<SimpleTestAtomicReadModel>.Filter.Empty).ConfigureAwait(false);
            Assert.That(totalCount, Is.GreaterThanOrEqualTo(100));
        }

        [Test]
        public async Task UpsertBatch_should_handle_concurrent_modifications_gracefully()
        {
            // Arrange: Create initial readmodels
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 5; i++)
            {
                _aggregateIdSeed = 1100 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                readmodels.Add(rm);
            }

            await _sut.UpsertBatchAsync(readmodels).ConfigureAwait(false);

            // Update readmodels for second batch - must maintain correct aggregate IDs
            var updatedReadmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < readmodels.Count; i++)
            {
                _aggregateIdSeed = 1100 + i; // Reset to correct aggregate ID
                var touchEvent = GenerateTouchedEvent(false);
                readmodels[i].ProcessChangeset(touchEvent);
                updatedReadmodels.Add(readmodels[i]);
            }

            // Act: Perform multiple concurrent batch upserts (simulating concurrent operations)
            var task1 = _sut.UpsertBatchAsync(updatedReadmodels);
            var task2 = _sut.UpsertBatchAsync(updatedReadmodels);

            // Should not throw
            await Task.WhenAll(task1, task2).ConfigureAwait(false);

            // Assert: All readmodels should be updated correctly
            foreach (var rm in updatedReadmodels)
            {
                var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.TouchCount, Is.EqualTo(1));
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion));
            }
        }

        [Test]
        public async Task UpsertBatch_with_single_readmodel_should_work()
        {
            // Arrange: Single readmodel in batch
            _aggregateIdSeed = 1200;
            _aggregateVersion = 1;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);

            var singleItemList = new List<SimpleTestAtomicReadModel> { rm };

            // Act
            await _sut.UpsertBatchAsync(singleItemList).ConfigureAwait(false);

            // Assert
            var reloaded = await _sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.Id, Is.EqualTo(rm.Id));
        }

        [Test]
        public void UpsertBatch_should_throw_on_duplicate_ids_in_batch()
        {
            // Arrange: Create batch with duplicate IDs
            _aggregateIdSeed = 1300;
            _aggregateVersion = 1;
            
            var rm1 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset1 = GenerateCreatedEvent(false);
            rm1.ProcessChangeset(changeset1);

            // Create another with same ID but higher version
            var rm2 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            rm2.ProcessChangeset(changeset1);
            var touchEvent = GenerateTouchedEvent(false);
            rm2.ProcessChangeset(touchEvent);

            var batch = new List<SimpleTestAtomicReadModel> { rm1, rm2 };

            // Act & Assert: Should throw exception for duplicate IDs
            var ex = Assert.ThrowsAsync<CollectionWrapperException>(() => _sut.UpsertBatchAsync(batch));
            Assert.That(ex.Message, Does.Contain("Duplicate readmodel Ids found in batch"));
            Assert.That(ex.Message, Does.Contain(rm1.Id));
        }

        #endregion
    }
}

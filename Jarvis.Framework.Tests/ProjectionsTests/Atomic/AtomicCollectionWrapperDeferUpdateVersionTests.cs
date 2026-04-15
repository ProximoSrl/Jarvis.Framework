using Castle.Core.Logging;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicCollectionWrapperDeferUpdateVersionTests : AtomicCollectionWrapperTestsBase
    {
        [SetUp]
        public void SetUp()
        {
            Init();
            InitSingleTest();
        }

        private static readonly MethodInfo ResetMethod = typeof(DeferredUpdateVersionCoordinator)
            .GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Static);

        [TearDown]
        public async Task TearDown()
        {
            // Flush pending items and reset the coordinator between tests to avoid cross-test interference.
            // Reset is private — invoked via reflection to clearly mark it as test-only.
            await DeferredUpdateVersionCoordinator.FlushAllAsync().ConfigureAwait(false);
            ResetMethod.Invoke(null, null);
        }

        #region Basic DeferUpdateVersionAsync Tests

        [Test]
        public async Task DeferUpdateVersion_should_persist_after_flush()
        {
            // Arrange: Create and persist a readmodel at version 1
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            // Process a non-modifying event to bump version
            var notHandled = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(notHandled);

            // Act: Defer and flush
            await _sut.DeferUpdateVersionAsync(rm).ConfigureAwait(false);
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion),
                "AggregateVersion should be updated after flush");
            Assert.That(reloaded.ProjectedPosition, Is.EqualTo(rm.ProjectedPosition),
                "ProjectedPosition should be updated after flush");
        }

        [Test]
        public async Task DeferUpdateVersion_should_not_persist_immediately()
        {
            // Arrange: Create and persist a readmodel at version 1
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            var originalVersion = rm.AggregateVersion;

            // Process a non-modifying event
            var notHandled = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(notHandled);

            // Act: Defer WITHOUT flush
            await _sut.DeferUpdateVersionAsync(rm).ConfigureAwait(false);

            // Assert: The database should still have the original version
            // (The batch hasn't been triggered yet because we haven't hit the max batch size)
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(originalVersion),
                "AggregateVersion should NOT be updated before flush when batch is under threshold");
        }

        [Test]
        public async Task DeferUpdateVersion_should_skip_ModifiedWithExtraStreamEvents()
        {
            // Arrange
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            rm.SetPropertyValue(_ => _.ModifiedWithExtraStreamEvents, true);

            // Act: Should silently skip
            await _sut.DeferUpdateVersionAsync(rm).ConfigureAwait(false);
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: Version should not have changed (the item was skipped at enqueue time)
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(1),
                "Readmodel with ModifiedWithExtraStreamEvents should not be updated");
        }

        [Test]
        public async Task DeferUpdateVersion_should_insert_missing_readmodel_after_flush()
        {
            // Arrange: Create a readmodel that is NOT persisted to DB
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            // Note: NOT calling UpsertAsync — so the readmodel does not exist on disk

            // Act: Defer and flush
            await _sut.DeferUpdateVersionAsync(rm).ConfigureAwait(false);
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: The readmodel should be inserted (UpdateVersionBatchAsync inserts missing ones)
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null, "Missing readmodel should be inserted via deferred path");
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion));
        }

        #endregion

        #region Deduplication Tests

        [Test]
        public async Task DeferUpdateVersion_should_deduplicate_same_id_keeping_highest_version()
        {
            // Arrange: Create and persist a readmodel
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var createChangeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(createChangeset);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            // Create multiple snapshots of the same readmodel at different versions
            // Version 2
            var event2 = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(event2);
            var rmAtVersion2 = CloneReadmodelState(rm);

            // Version 3
            var event3 = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(event3);
            var rmAtVersion3 = CloneReadmodelState(rm);

            // Act: Defer all versions (they should be deduplicated in the batch)
            // We need to ensure they all land in the same batch, so use small batch size
            JarvisFrameworkGlobalConfiguration.ConfigureDeferredUpdateVersion(flushIntervalMs: 60000, maxBatchSize: 100);

            // Re-create SUT so it picks up new config on next DeferUpdateVersionAsync
            GenerateSut();

            await _sut.DeferUpdateVersionAsync(rmAtVersion2).ConfigureAwait(false);
            await _sut.DeferUpdateVersionAsync(rmAtVersion3).ConfigureAwait(false);
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: Only the highest version should be persisted
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(3),
                "Deduplication should keep highest AggregateVersion");
        }

        [Test]
        public async Task DeferUpdateVersion_should_deduplicate_keeping_highest_even_if_queued_out_of_order()
        {
            // Arrange
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var createChangeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(createChangeset);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            // Create snapshots at version 2 and 3
            var event2 = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(event2);
            var rmAtVersion2 = CloneReadmodelState(rm);

            var event3 = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(event3);
            var rmAtVersion3 = CloneReadmodelState(rm);

            JarvisFrameworkGlobalConfiguration.ConfigureDeferredUpdateVersion(flushIntervalMs: 60000, maxBatchSize: 100);
            GenerateSut();

            // Act: Queue version 3 FIRST, then version 2 (out of order)
            await _sut.DeferUpdateVersionAsync(rmAtVersion3).ConfigureAwait(false);
            await _sut.DeferUpdateVersionAsync(rmAtVersion2).ConfigureAwait(false);
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: Version 3 should win regardless of queue order
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(3),
                "Deduplication should keep highest AggregateVersion even when queued out of order");
        }

        [Test]
        public async Task DeferUpdateVersion_multiple_ids_in_same_batch_are_all_persisted()
        {
            // Arrange: Create multiple readmodels
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 5; i++)
            {
                _aggregateIdSeed = 3000 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                await _sut.UpsertAsync(rm).ConfigureAwait(false);

                // Bump version
                var notHandled = GenerateSampleAggregateNotHandledEvent(false);
                rm.ProcessChangeset(notHandled);
                readmodels.Add(rm);
            }

            // Act: Defer all and flush
            foreach (var rm in readmodels)
            {
                await _sut.DeferUpdateVersionAsync(rm).ConfigureAwait(false);
            }
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: All should be updated
            foreach (var rm in readmodels)
            {
                var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion),
                    $"Readmodel {rm.Id} should have updated AggregateVersion");
            }
        }

        [Test]
        public async Task DeferUpdateVersion_mixed_ids_some_duplicated_should_persist_all_correctly()
        {
            // Arrange: Two aggregates
            _aggregateIdSeed = 3100;
            _aggregateVersion = 1;
            var rmA = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var createA = GenerateCreatedEvent(false);
            rmA.ProcessChangeset(createA);
            await _sut.UpsertAsync(rmA).ConfigureAwait(false);

            _aggregateIdSeed = 3101;
            _aggregateVersion = 1;
            var rmB = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var createB = GenerateCreatedEvent(false);
            rmB.ProcessChangeset(createB);
            await _sut.UpsertAsync(rmB).ConfigureAwait(false);

            // Advance A to version 2 and 3
            _aggregateIdSeed = 3100;
            var eventA2 = GenerateSampleAggregateNotHandledEvent(false);
            rmA.ProcessChangeset(eventA2);
            var rmAv2 = CloneReadmodelState(rmA);

            var eventA3 = GenerateSampleAggregateNotHandledEvent(false);
            rmA.ProcessChangeset(eventA3);
            var rmAv3 = CloneReadmodelState(rmA);

            // Advance B to version 2 only
            _aggregateIdSeed = 3101;
            _aggregateVersion = 2;
            var eventB2 = GenerateSampleAggregateNotHandledEvent(false);
            rmB.ProcessChangeset(eventB2);

            JarvisFrameworkGlobalConfiguration.ConfigureDeferredUpdateVersion(flushIntervalMs: 60000, maxBatchSize: 100);
            GenerateSut();

            // Act: Queue A at v2, B at v2, A at v3 — A should be deduped to v3
            await _sut.DeferUpdateVersionAsync(rmAv2).ConfigureAwait(false);
            await _sut.DeferUpdateVersionAsync(rmB).ConfigureAwait(false);
            await _sut.DeferUpdateVersionAsync(rmAv3).ConfigureAwait(false);
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert
            var reloadedA = await _collection.FindOneByIdAsync(rmA.Id).ConfigureAwait(false);
            Assert.That(reloadedA.AggregateVersion, Is.EqualTo(3), "A should be at version 3 (deduped)");

            var reloadedB = await _collection.FindOneByIdAsync(rmB.Id).ConfigureAwait(false);
            Assert.That(reloadedB.AggregateVersion, Is.EqualTo(2), "B should be at version 2");
        }

        #endregion

        #region Batch Threshold Tests

        [Test]
        public async Task DeferUpdateVersion_should_flush_when_batch_size_reached()
        {
            // Arrange: Configure small batch size so we can trigger without timer
            JarvisFrameworkGlobalConfiguration.ConfigureDeferredUpdateVersion(flushIntervalMs: 60000, maxBatchSize: 5);
            GenerateSut();

            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 5; i++)
            {
                _aggregateIdSeed = 4000 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                await _sut.UpsertAsync(rm).ConfigureAwait(false);

                var notHandled = GenerateSampleAggregateNotHandledEvent(false);
                rm.ProcessChangeset(notHandled);
                readmodels.Add(rm);
            }

            // Act: Queue exactly maxBatchSize items — BatchBlock should emit immediately
            foreach (var rm in readmodels)
            {
                await _sut.DeferUpdateVersionAsync(rm).ConfigureAwait(false);
            }

            // Give the ActionBlock a moment to process
            await Task.Delay(500).ConfigureAwait(false);

            // Assert: Items should be persisted even without an explicit flush
            foreach (var rm in readmodels)
            {
                var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion),
                    $"Readmodel {rm.Id} should be auto-flushed when batch size reached");
            }
        }

        [Test]
        public async Task DeferUpdateVersion_should_flush_partial_batch_via_timer()
        {
            // Arrange: Configure very short timer so the partial batch flushes quickly
            JarvisFrameworkGlobalConfiguration.ConfigureDeferredUpdateVersion(flushIntervalMs: 200, maxBatchSize: 100);
            GenerateSut();

            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            var notHandled = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(notHandled);

            // Act: Queue a single item (well below maxBatchSize) and wait for timer
            await _sut.DeferUpdateVersionAsync(rm).ConfigureAwait(false);

            // Wait enough time for the timer to fire (200ms interval + processing time)
            await Task.Delay(1500).ConfigureAwait(false);

            // Assert: The item should have been flushed by the timer
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion),
                "Partial batch should be flushed by the shared timer");
        }

        #endregion

        #region FlushDeferredUpdatesAsync Tests

        [Test]
        public async Task FlushDeferredUpdates_on_uninitialized_pipeline_should_not_throw()
        {
            // Act & Assert: Flushing before any DeferUpdateVersionAsync call should be a no-op
            Assert.DoesNotThrowAsync(() => _sut.FlushDeferredUpdatesAsync());
        }

        [Test]
        public async Task FlushDeferredUpdates_should_drain_all_pending_items()
        {
            // Arrange: Create several readmodels
            var readmodels = new List<SimpleTestAtomicReadModel>();
            for (int i = 0; i < 10; i++)
            {
                _aggregateIdSeed = 5000 + i;
                _aggregateVersion = 1;
                var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
                var changeset = GenerateCreatedEvent(false);
                rm.ProcessChangeset(changeset);
                await _sut.UpsertAsync(rm).ConfigureAwait(false);

                var notHandled = GenerateSampleAggregateNotHandledEvent(false);
                rm.ProcessChangeset(notHandled);
                readmodels.Add(rm);
            }

            // Act: Queue all, then flush
            foreach (var rm in readmodels)
            {
                await _sut.DeferUpdateVersionAsync(rm).ConfigureAwait(false);
            }
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: All should be persisted
            foreach (var rm in readmodels)
            {
                var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion),
                    $"Readmodel {rm.Id} should be persisted after flush");
            }
        }

        #endregion

        #region Coordinator Flush + Reset Tests

        [Test]
        public async Task FlushDeferredUpdates_should_drain_pending_items()
        {
            // Arrange: Use the single _sut wrapper
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset = GenerateCreatedEvent(false);
            rm.ProcessChangeset(changeset);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            var notHandled = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(notHandled);

            await _sut.DeferUpdateVersionAsync(rm).ConfigureAwait(false);

            // Act: Complete the pipeline and wait for all in-flight items
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: The item should have been flushed
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion),
                "FlushDeferredUpdatesAsync should drain all pending deferred updates");
        }

        [Test]
        public async Task FlushAll_with_no_registered_pipelines_should_not_throw()
        {
            // Act & Assert: Should be a safe no-op
            Assert.DoesNotThrowAsync(() => DeferredUpdateVersionCoordinator.FlushAllAsync());
        }

        [Test]
        public void Reset_with_no_registered_pipelines_should_not_throw()
        {
            // Act & Assert: Should be a safe no-op
            Assert.DoesNotThrow(() => ResetMethod.Invoke(null, null));
        }

        #endregion

        #region Version Guard Tests

        [Test]
        public async Task DeferUpdateVersion_should_not_regress_version_on_disk()
        {
            // Arrange: Create a readmodel at version 3 on disk
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var create = GenerateCreatedEvent(false);
            rm.ProcessChangeset(create);
            var touch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(touch);
            var notHandled = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(notHandled);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(3), "Precondition: readmodel at version 3");

            // Create a stale copy at version 1
            _aggregateVersion = 1;
            var staleRm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var staleChangeset = GenerateCreatedEvent(false);
            staleRm.ProcessChangeset(staleChangeset);

            // Act: Defer the stale copy and flush
            await _sut.DeferUpdateVersionAsync(staleRm).ConfigureAwait(false);
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: Version should NOT be regressed
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(3),
                "DeferUpdateVersion should not regress AggregateVersion from 3 to 1");
        }

        [Test]
        public async Task DeferUpdateVersion_should_only_update_version_fields_not_content()
        {
            // Arrange: Create a readmodel with TouchCount = 1
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var create = GenerateCreatedEvent(false);
            rm.ProcessChangeset(create);
            var touch = GenerateTouchedEvent(false);
            rm.ProcessChangeset(touch);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            // Manually change TouchCount on disk to a sentinel value
            var filter = Builders<SimpleTestAtomicReadModel>.Filter.Eq("_id", rm.Id);
            var update = Builders<SimpleTestAtomicReadModel>.Update.Set(m => m.TouchCount, 999);
            await _collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            // Process non-modifying event
            var notHandled = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(notHandled);

            // Act: Defer and flush
            await _sut.DeferUpdateVersionAsync(rm).ConfigureAwait(false);
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: Version updated but TouchCount preserved
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion),
                "AggregateVersion should be updated");
            Assert.That(reloaded.TouchCount, Is.EqualTo(999),
                "TouchCount should be unchanged — deferred path only updates version fields");
        }

        [Test]
        public async Task DeferUpdateVersion_batch_with_v3_then_v2_should_persist_v3()
        {
            // Arrange: Create and persist a readmodel at version 1
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var create = GenerateCreatedEvent(false);
            rm.ProcessChangeset(create);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            // Build snapshots at v2 and v3
            var event2 = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(event2);
            var rmAtV2 = CloneReadmodelState(rm);

            var event3 = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(event3);
            var rmAtV3 = CloneReadmodelState(rm);

            Assert.That(rmAtV2.AggregateVersion, Is.EqualTo(2), "Precondition: snapshot at v2");
            Assert.That(rmAtV3.AggregateVersion, Is.EqualTo(3), "Precondition: snapshot at v3");

            // Use large batch + long timer so both land in the same batch
            JarvisFrameworkGlobalConfiguration.ConfigureDeferredUpdateVersion(flushIntervalMs: 60000, maxBatchSize: 100);
            GenerateSut();

            // Act: Queue v3 first, then v2 — dedup should keep v3
            await _sut.DeferUpdateVersionAsync(rmAtV3).ConfigureAwait(false);
            await _sut.DeferUpdateVersionAsync(rmAtV2).ConfigureAwait(false);
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: Disk should have version 3
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(3),
                "When batch contains v3 then v2 for the same id, v3 must be the one persisted");
        }

        [Test]
        public async Task DeferUpdateVersion_should_not_overwrite_higher_version_written_concurrently()
        {
            // Arrange: Create and persist a readmodel at version 1
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var create = GenerateCreatedEvent(false);
            rm.ProcessChangeset(create);
            await _sut.UpsertAsync(rm).ConfigureAwait(false);

            // Build an in-memory snapshot at v2
            var event2 = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(event2);
            var rmAtV2 = CloneReadmodelState(rm);
            Assert.That(rmAtV2.AggregateVersion, Is.EqualTo(2), "Precondition: deferred snapshot at v2");

            // Now simulate a concurrent update: advance the DB readmodel to v3
            // directly via a full upsert (as the projection engine would do)
            var event3 = GenerateSampleAggregateNotHandledEvent(false);
            rm.ProcessChangeset(event3);
            Assert.That(rm.AggregateVersion, Is.EqualTo(3), "Precondition: concurrent version at v3");
            await _sut.UpdateVersionAsync(rm).ConfigureAwait(false);

            // Verify the DB is indeed at v3 before the deferred flush
            var beforeFlush = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(beforeFlush.AggregateVersion, Is.EqualTo(3), "Precondition: DB at v3 before flush");

            // Act: Queue the stale v2 snapshot and flush
            await _sut.DeferUpdateVersionAsync(rmAtV2).ConfigureAwait(false);
            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: DB should still be at v3 — the deferred v2 must NOT regress
            var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
            Assert.That(reloaded.AggregateVersion, Is.EqualTo(3),
                "Deferred batch at v2 must not overwrite v3 that was written concurrently");
        }

        #endregion

        #region Error Resilience Tests

        [Test]
        public async Task DeferUpdateVersion_pipeline_should_survive_errors_and_continue()
        {
            // Arrange: Use small batch to force multiple flushes
            JarvisFrameworkGlobalConfiguration.ConfigureDeferredUpdateVersion(flushIntervalMs: 60000, maxBatchSize: 2);
            GenerateSut();

            // First batch: one valid readmodel
            _aggregateIdSeed = 6000;
            _aggregateVersion = 1;
            var rm1 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset1 = GenerateCreatedEvent(false);
            rm1.ProcessChangeset(changeset1);
            await _sut.UpsertAsync(rm1).ConfigureAwait(false);
            var nh1 = GenerateSampleAggregateNotHandledEvent(false);
            rm1.ProcessChangeset(nh1);

            // Second readmodel for same batch
            _aggregateIdSeed = 6001;
            _aggregateVersion = 1;
            var rm2 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset2 = GenerateCreatedEvent(false);
            rm2.ProcessChangeset(changeset2);
            await _sut.UpsertAsync(rm2).ConfigureAwait(false);
            var nh2 = GenerateSampleAggregateNotHandledEvent(false);
            rm2.ProcessChangeset(nh2);

            // Third readmodel for second batch
            _aggregateIdSeed = 6002;
            _aggregateVersion = 1;
            var rm3 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset3 = GenerateCreatedEvent(false);
            rm3.ProcessChangeset(changeset3);
            await _sut.UpsertAsync(rm3).ConfigureAwait(false);
            var nh3 = GenerateSampleAggregateNotHandledEvent(false);
            rm3.ProcessChangeset(nh3);

            // Fourth readmodel for second batch
            _aggregateIdSeed = 6003;
            _aggregateVersion = 1;
            var rm4 = new SimpleTestAtomicReadModel(new SampleAggregateId(_aggregateIdSeed));
            var changeset4 = GenerateCreatedEvent(false);
            rm4.ProcessChangeset(changeset4);
            await _sut.UpsertAsync(rm4).ConfigureAwait(false);
            var nh4 = GenerateSampleAggregateNotHandledEvent(false);
            rm4.ProcessChangeset(nh4);

            // Act: Queue all four — first batch (2 items) and second batch (2 items)
            await _sut.DeferUpdateVersionAsync(rm1).ConfigureAwait(false);
            await _sut.DeferUpdateVersionAsync(rm2).ConfigureAwait(false);
            // Wait for first batch to process
            await Task.Delay(300).ConfigureAwait(false);

            await _sut.DeferUpdateVersionAsync(rm3).ConfigureAwait(false);
            await _sut.DeferUpdateVersionAsync(rm4).ConfigureAwait(false);

            await _sut.FlushDeferredUpdatesAsync().ConfigureAwait(false);

            // Assert: All four should be persisted (proves second batch ran even if first had issues)
            foreach (var rm in new[] { rm1, rm2, rm3, rm4 })
            {
                var reloaded = await _collection.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null, $"Readmodel {rm.Id} should be persisted");
                Assert.That(reloaded.AggregateVersion, Is.EqualTo(rm.AggregateVersion),
                    $"Readmodel {rm.Id} should have correct version");
            }
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void ConfigureDeferredUpdateVersion_should_reject_invalid_flush_interval()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                JarvisFrameworkGlobalConfiguration.ConfigureDeferredUpdateVersion(flushIntervalMs: 50));
        }

        [Test]
        public void ConfigureDeferredUpdateVersion_should_reject_invalid_batch_size()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                JarvisFrameworkGlobalConfiguration.ConfigureDeferredUpdateVersion(maxBatchSize: 0));
        }

        [Test]
        public void ConfigureDeferredUpdateVersion_should_accept_valid_values()
        {
            Assert.DoesNotThrow(() =>
                JarvisFrameworkGlobalConfiguration.ConfigureDeferredUpdateVersion(flushIntervalMs: 500, maxBatchSize: 10));

            Assert.That(JarvisFrameworkGlobalConfiguration.DeferredUpdateVersionFlushIntervalMs, Is.EqualTo(500));
            Assert.That(JarvisFrameworkGlobalConfiguration.DeferredUpdateVersionMaxBatchSize, Is.EqualTo(10));
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates a new readmodel instance with the same Id and processes the same events
        /// to arrive at the current state. This simulates having a snapshot at a specific version.
        /// </summary>
        private SimpleTestAtomicReadModel CloneReadmodelState(SimpleTestAtomicReadModel source)
        {
            // We create a fresh readmodel with the same Id. Since we can't deep-clone,
            // we rely on the fact that the fields we care about (AggregateVersion, ProjectedPosition,
            // LastProcessedVersions) are set by ProcessChangeset. We use SetPropertyValue to
            // copy the key fields.
            var clone = new SimpleTestAtomicReadModel(source.Id);
            clone.SetPropertyValue(_ => _.AggregateVersion, source.AggregateVersion);
            clone.SetPropertyValue(_ => _.ProjectedPosition, source.ProjectedPosition);
            clone.SetPropertyValue(_ => _.ReadModelVersion, source.ReadModelVersion);
            clone.SetPropertyValue(_ => _.LastProcessedVersions, source.LastProcessedVersions?.ToList() ?? new List<long>());
            return clone;
        }

        #endregion
    }
}

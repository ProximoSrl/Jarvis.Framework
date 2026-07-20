using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Support;
using Machine.Specifications;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    /// <summary>
    /// VErify both the <see cref="ConcurrentCheckpointTracker"/> and the <see cref="SlotStatusManager"/>
    /// classes
    /// </summary>
    [TestFixture]
    public class ConcurrentCheckpointTrackerTests
    {
        private IMongoDatabase _db;
        private ConcurrentCheckpointTracker _concurrentCheckpointTrackerSut;
        private SlotStatusManager _slotStatusCheckerSut;
        private IMongoCollection<Checkpoint> _checkPoints;
        private string _storeDir;
        private List<ConcurrentCheckpointTracker> _trackersToDispose;
        private List<string> _seedFoldersToDelete;

        [SetUp]
        public void SetUp()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url.CreateMongoClientSettings());
            _db = client.GetDatabase(url.DatabaseName);
            _checkPoints = _db.GetCollection<Checkpoint>("checkpoints");
            _checkPoints.Drop();
            _slotStatusCheckerSut = null;
            _concurrentCheckpointTrackerSut = null;
            // Each test gets its own local durable-store directory so the crash-safe files never
            // leak checkpoints across tests (MongoDB is dropped above, the local store must too).
            _storeDir = Path.Combine(Path.GetTempPath(), "jarvis-fw-checkpoint-tests", Guid.NewGuid().ToString("N"));
            _trackersToDispose = new List<ConcurrentCheckpointTracker>();
            _seedFoldersToDelete = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var tracker in _trackersToDispose)
            {
                try { tracker.Dispose(); } catch { /* best effort */ }
            }
            _trackersToDispose.Clear();
            var foldersToDelete = new List<string> { _storeDir };
            foldersToDelete.AddRange(_seedFoldersToDelete);
            foreach (var folder in foldersToDelete)
            {
                try
                {
                    if (folder != null && Directory.Exists(folder))
                    {
                        Directory.Delete(folder, recursive: true);
                    }
                }
                catch { /* best effort */ }
            }
        }

        private static string GetSeedFolder(string seed)
        {
            return Path.Combine(Path.GetTempPath(), "jarvis-framework-checkpoints", seed);
        }

        /// <summary>
        /// Builds a tracker against the test collection, injecting a durable store that points at an
        /// isolated per-test directory. The flush timer interval is long (60s) so it never fires
        /// during a test; tests drive <see cref="ConcurrentCheckpointTracker.FlushCheckpointAsync"/>
        /// explicitly for deterministic assertions.
        /// </summary>
        private ConcurrentCheckpointTracker CreateTracker(int flushTimeoutSeconds = 60)
        {
            var tracker = new ConcurrentCheckpointTracker(
                _checkPoints,
                flushTimeoutSeconds,
                new FileSystemCheckpointDurableStore(_storeDir));
            _trackersToDispose.Add(tracker);
            return tracker;
        }

        [Test]
        public void Verify_check_of_new_projection()
        {
            SetupOneProjectionNew();
            RebuildSettings.Init(false, false);
            var errors = _slotStatusCheckerSut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors.ElementAt(0), Is.EqualTo("Error in slot default: we have new projections at checkpoint 0.\n REBUILD NEEDED!"));
        }

        [Test]
        public void Verify_check_of_signature_change()
        {
            RebuildSettings.Init(false, false);
            SetupOneProjectionChangedSignature();
            var errors = _slotStatusCheckerSut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors.ElementAt(0), Is.EqualTo("Projection Projection2 [slot default] has signature V2 but checkpoint on database has signature oldSignature.\n REBUILD NEEDED"));
        }

        [Test]
        public void Verify_current_null_should_be_rebuilded()
        {
            RebuildSettings.Init(false, false);
            SetupOneProjectionWithCurrentNull();
            var errors = _slotStatusCheckerSut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors.ElementAt(0), Is.EqualTo("Projection Projection [slot default] had an interrupted rebuild because current is 0 and value is 42 \n REBUILD NEEDED"));
        }

        [Test]
        public void Verify_check_of_signature_change_during_rebuild()
        {
            RebuildSettings.Init(true, true);
            SetupOneProjectionChangedSignature();
            var errors = _slotStatusCheckerSut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(0));
        }

        [Test]
        public void Verify_check_of_generic_projection_checkpoint_error()
        {
            RebuildSettings.Init(false, false);
            SetupTwoProjectionsError();
            var errors = _slotStatusCheckerSut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors.ElementAt(0), Is.EqualTo("Error in slot default, not all projection at the same checkpoint value. Please check reamodel db!"));
        }

        [Test]
        public void Verify_check_of_new_projection_ok_when_rebuild()
        {
            SetupOneProjectionNew();
            RebuildSettings.Init(true, false);
            var errors = _slotStatusCheckerSut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(0));
        }

        [Test]
        public void Verify_slot_status_all_ok_with_no_checkpoints()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projection2 = new Projection2(Substitute.For<ICollectionWrapper<SampleReadModel2, String>>());
            //A projection in other slot
            var projection3 = new Projection3(Substitute.For<ICollectionWrapper<SampleReadModel3, String>>());

            var projections = new IProjection[] { projection1, projection2, projection3 };

            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();

            Assert.That(status.AllSlots, Has.Count.EqualTo(2));
        }

        /// <summary>
        /// This will verify the need to rebuild a slot when current is null and value is greater than zero
        /// this identify a situation where a rebuild was interrupted
        /// </summary>
        [Test]
        public void Verify_slot_status_for_interrupted_rebuilds()
        {
            //TSingle projection
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());

            var projections = new IProjection[] { projection1 };
            var checkpoint = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature);
            checkpoint.Value = 32;
            checkpoint.Current = null;
            _checkPoints.InsertMany(new[] { checkpoint });

            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();

            Assert.That(status.AllSlots, Has.Count.EqualTo(1));
            Assert.That(status.SlotsThatNeedsRebuild, Is.EquivalentTo(new[] { projection1.Info.SlotName}));
        }

        [Test]
        public void Verify_slot_status_all_ok()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projection2 = new Projection2(Substitute.For<ICollectionWrapper<SampleReadModel2, String>>());
            //A projection in other slot
            var projection3 = new Projection3(Substitute.For<ICollectionWrapper<SampleReadModel3, String>>());

            var projections = new IProjection[] { projection1, projection2, projection3 };
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature);
            var checkpoint2 = new Checkpoint(projection2.Info.CommonName, 1, projection2.Info.Signature);
            var checkpoint3 = new Checkpoint(projection3.Info.CommonName, 1, projection3.Info.Signature);
            checkpoint1.Current = checkpoint1.Value;
            checkpoint2.Current = checkpoint2.Value;
            checkpoint3.Current = checkpoint3.Value;
            _checkPoints.InsertMany(new[] { checkpoint1, checkpoint2, checkpoint3 });

            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();

            Assert.That(status.NewSlots, Has.Count.EqualTo(0));
            Assert.That(status.SlotsThatNeedsRebuild, Has.Count.EqualTo(0));
            Assert.That(status.AllSlots, Has.Count.EqualTo(2));
        }

        [Test]
        public void Verify_projection_change_info_all_ok()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projection2 = new Projection2(Substitute.For<ICollectionWrapper<SampleReadModel2, String>>());
            //A projection in other slot
            var projection3 = new Projection3(Substitute.For<ICollectionWrapper<SampleReadModel3, String>>());

            var projections = new IProjection[] { projection1, projection2, projection3 };
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature) { Slot = projection1.Info.SlotName };
            var checkpoint2 = new Checkpoint(projection2.Info.CommonName, 1, projection2.Info.Signature) { Slot = projection2.Info.SlotName };
            var checkpoint3 = new Checkpoint(projection3.Info.CommonName, 1, projection3.Info.Signature) { Slot = projection3.Info.SlotName };
            _checkPoints.InsertMany(new[] { checkpoint1, checkpoint2, checkpoint3 });

            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetProjectionChangeInfo();

            Assert.That(status.Count(), Is.EqualTo(3));
            Assert.That(status.All(s => s.ChangeType == ProjectionChangeInfo.ProjectionChangeType.None));
        }

        [Test]
        public void Verify_slot_status_new_projection()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projection2 = new Projection2(Substitute.For<ICollectionWrapper<SampleReadModel2, String>>());
            //A projection in other slot
            var projection3 = new Projection3(Substitute.For<ICollectionWrapper<SampleReadModel3, String>>());

            var projections = new IProjection[] { projection1, projection2, projection3 };
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature);
            var checkpoint2 = new Checkpoint(projection2.Info.CommonName, 1, projection2.Info.Signature);

            checkpoint1.Current = checkpoint1.Value;
            checkpoint2.Current = checkpoint2.Value;

            _checkPoints.InsertMany(new[] { checkpoint1, checkpoint2, });

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();

            Assert.That(status.NewSlots, Is.EquivalentTo(new[] { projection3.Info.SlotName }));

            Assert.That(status.SlotsThatNeedsRebuild, Has.Count.EqualTo(0));
        }

        [Test]
        public void Verify_projection_change_info_new_projection()
        {
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projection2 = new Projection2(Substitute.For<ICollectionWrapper<SampleReadModel2, String>>());
            //A projection in other slot
            var projection3 = new Projection3(Substitute.For<ICollectionWrapper<SampleReadModel3, String>>());

            var projections = new IProjection[] { projection1, projection2, projection3 };
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature) { Slot = projection1.Info.SlotName };
            var checkpoint2 = new Checkpoint(projection2.Info.CommonName, 1, projection2.Info.Signature) { Slot = projection2.Info.SlotName };

            _checkPoints.InsertMany(new[] { checkpoint1, checkpoint2, });

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetProjectionChangeInfo();

            Assert.That(status.Count(), Is.EqualTo(3));
            var none = status.Where(s => s.ChangeType == ProjectionChangeInfo.ProjectionChangeType.None);
            Assert.That(none.Count(), Is.EqualTo(2));
            var singleChange = status.Single(s => s.ChangeType == ProjectionChangeInfo.ProjectionChangeType.NewProjection);
            Assert.That(singleChange.CommonName, Is.EqualTo(projection3.Info.CommonName));
        }

        [Test]
        public void Verify_new_projection_when_zero_events_dispatched()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projection2 = new Projection2(Substitute.For<ICollectionWrapper<SampleReadModel2, String>>());
            //A projection in other slot
            var projection3 = new Projection3(Substitute.For<ICollectionWrapper<SampleReadModel3, String>>());

            var projections = new IProjection[] { projection1, projection2, projection3 };
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature);
            var checkpoint2 = new Checkpoint(projection2.Info.CommonName, 1, projection2.Info.Signature);
            var checkpoint3 = new Checkpoint(projection3.Info.CommonName, 0, projection3.Info.Signature);

            _checkPoints.InsertMany(new[] { checkpoint1, checkpoint2, checkpoint3 });

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();

            Assert.That(status.NewSlots, Is.EquivalentTo(new[] { projection3.Info.SlotName }));
        }

        [Test]
        public void Verify_slot_status_new_projection_when_db_empty()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projection2 = new Projection2(Substitute.For<ICollectionWrapper<SampleReadModel2, String>>());
            //A projection in other slot
            var projection3 = new Projection3(Substitute.For<ICollectionWrapper<SampleReadModel3, String>>());

            var projections = new IProjection[] { projection1, projection2, projection3 };

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();

            Assert.That(status.NewSlots, Has.Count.EqualTo(0));

            Assert.That(status.SlotsThatNeedsRebuild, Has.Count.EqualTo(0));
        }

        [Test]
        public void Verify_status_for_slot_that_needs_to_be_rebuilded()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());

            var projections = new IProjection[] { projection1 };
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature + "modified");

            _checkPoints.InsertMany(new[] { checkpoint1, });

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();
            Assert.That(status.SlotsThatNeedsRebuild, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Verify_dispatched_event_are_flushed()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());

            var projections = new IProjection[] { projection1 };

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);
            await _concurrentCheckpointTrackerSut.UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 891, true).ConfigureAwait(false);

            //In deferred mode the dispatched checkpoint reaches MongoDB only on flush.
            await _concurrentCheckpointTrackerSut.FlushCheckpointAsync().ConfigureAwait(false);

            var allDbCheckpoints = _checkPoints.AsQueryable().Where(c => c.Slot == "default").ToList();
            Assert.That(allDbCheckpoints.Single().Slot, Is.EqualTo("default"));
            Assert.That(allDbCheckpoints.Single().Current, Is.EqualTo(891));
            Assert.That(allDbCheckpoints.Single().Value, Is.EqualTo(891));
        }

        [Test]
        public async Task Verify_out_of_order_checkpoint_cannot_regress_durable_slot()
        {
            var projection = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projections = new IProjection[] { projection };

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);
            await _concurrentCheckpointTrackerSut
                .UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 100, true)
                .ConfigureAwait(false);
            await _concurrentCheckpointTrackerSut
                .UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 90, true)
                .ConfigureAwait(false);

            await _concurrentCheckpointTrackerSut.FlushCheckpointAsync().ConfigureAwait(false);

            var durableCheckpoint = _checkPoints.AsQueryable().Single(checkpoint => checkpoint.Slot == "default");
            Assert.That(durableCheckpoint.Current, Is.EqualTo(100));
            Assert.That(durableCheckpoint.Value, Is.EqualTo(100));
            Assert.That(_concurrentCheckpointTrackerSut.GetCheckpoint(projection), Is.EqualTo(100));
        }

        [Test]
        public async Task Verify_updates_are_not_written_to_mongo_until_flush()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());

            var projections = new IProjection[] { projection1 };

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);

            //In deferred mode neither a dispatched nor a non-dispatched update touches MongoDB
            //on the hot path: they go to the local durable medium and the in-memory value only.
            await _concurrentCheckpointTrackerSut.UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 891, true).ConfigureAwait(false);
            await _concurrentCheckpointTrackerSut.UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 892, false).ConfigureAwait(false);

            var beforeFlush = _checkPoints.AsQueryable().Where(c => c.Slot == "default").ToList();
            Assert.That(beforeFlush.Single().Current ?? 0, Is.EqualTo(0), "MongoDB must not be updated before a flush");

            //in memory checkpoint reflects the latest value immediately.
            Assert.That(_concurrentCheckpointTrackerSut.GetCheckpoint(projections[0]), Is.EqualTo(892));

            //after a flush the latest value is durably persisted to MongoDB.
            await _concurrentCheckpointTrackerSut.FlushCheckpointAsync().ConfigureAwait(false);
            var afterFlush = _checkPoints.AsQueryable().Where(c => c.Slot == "default").ToList();
            Assert.That(afterFlush.Single().Current, Is.EqualTo(892));
            Assert.That(afterFlush.Single().Value, Is.EqualTo(892));
        }

        [Test]
        public async Task Verify_non_dispatched_event_can_be_flushed()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());

            var projections = new IProjection[] { projection1 };

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);
            await _concurrentCheckpointTrackerSut.UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 891, true).ConfigureAwait(false);

            //now update slot telling that the event was not dispatched, then flush immediately.
            await _concurrentCheckpointTrackerSut.UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 892, false).ConfigureAwait(false);
            await _concurrentCheckpointTrackerSut.FlushCheckpointAsync().ConfigureAwait(false);

            var allDbCheckpoints = _checkPoints.AsQueryable().Where(c => c.Slot == "default").ToList();
            Assert.That(allDbCheckpoints.Single().Slot, Is.EqualTo("default"));
            Assert.That(allDbCheckpoints.Single().Current, Is.EqualTo(892));
            Assert.That(allDbCheckpoints.Single().Value, Is.EqualTo(892));

            //in memory checkpoint should be updated.
            Assert.That(_concurrentCheckpointTrackerSut.GetCheckpoint(projections[0]), Is.EqualTo(892));
        }

        [Test]
        public async Task Verify_non_dispatched_event_can_be_flushed_even_at_startup()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());

            var projections = new IProjection[] { projection1 };

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);

            //We dispatch a single event not tracked, current is still null, then we want to flush
            await _concurrentCheckpointTrackerSut.UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 892, false).ConfigureAwait(false);
            await _concurrentCheckpointTrackerSut.FlushCheckpointAsync().ConfigureAwait(false);

            var allDbCheckpoints = _checkPoints.AsQueryable().Where(c => c.Slot == "default").ToList();
            Assert.That(allDbCheckpoints.Single().Slot, Is.EqualTo("default"));
            Assert.That(allDbCheckpoints.Single().Current, Is.EqualTo(892));
            Assert.That(allDbCheckpoints.Single().Value, Is.EqualTo(892));

            //in memory checkpoint should be updated.
            Assert.That(_concurrentCheckpointTrackerSut.GetCheckpoint(projections[0]), Is.EqualTo(892));
        }

        [Test]
        public async Task Rebuild_started_resets_current_and_flush_does_not_resurrect_it()
        {
            var projection = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var tracker = CreateTracker(60);
            tracker.SetUp(new[] { projection }, 1, false);

            await tracker.UpdateSlotAndSetCheckpointAsync(
                "default",
                new[] { projection.Info.CommonName },
                42,
                true).ConfigureAwait(false);

            // RebuildStarted resets Current to null. It must fence the flush and lower the local
            // state so a subsequent flush cannot write the pre-rebuild token back.
            tracker.RebuildStarted(projection, 0);
            await tracker.FlushCheckpointAsync().ConfigureAwait(false);

            var checkpoint = _checkPoints.FindOneById(projection.Info.CommonName);
            Assert.That(checkpoint.Current, Is.Null);
        }

        [Test]
        public async Task Verify_flush_persists_latest_checkpoint_to_mongo()
        {
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projections = new IProjection[] { projection1 };

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);

            await _concurrentCheckpointTrackerSut.UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 891, true).ConfigureAwait(false);
            await _concurrentCheckpointTrackerSut.FlushCheckpointAsync().ConfigureAwait(false);

            var afterFirstFlush = _checkPoints.AsQueryable().Where(c => c.Slot == "default").ToList();
            Assert.That(afterFirstFlush.Single().Current, Is.EqualTo(891));
            Assert.That(afterFirstFlush.Single().Value, Is.EqualTo(891));

            //A later non-dispatched advance is not persisted until the next flush.
            await _concurrentCheckpointTrackerSut.UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 892, false).ConfigureAwait(false);
            var beforeSecondFlush = _checkPoints.AsQueryable().Where(c => c.Slot == "default").ToList();
            Assert.That(beforeSecondFlush.Single().Current, Is.EqualTo(891));
            Assert.That(_concurrentCheckpointTrackerSut.GetCheckpoint(projections[0]), Is.EqualTo(892));

            await _concurrentCheckpointTrackerSut.FlushCheckpointAsync().ConfigureAwait(false);
            var afterSecondFlush = _checkPoints.AsQueryable().Where(c => c.Slot == "default").ToList();
            Assert.That(afterSecondFlush.Single().Current, Is.EqualTo(892));
            Assert.That(afterSecondFlush.Single().Value, Is.EqualTo(892));
            Assert.That(_concurrentCheckpointTrackerSut.GetCheckpoint(projections[0]), Is.EqualTo(892));
        }

        [Test]
        public async Task Restart_reconciles_from_durable_store_when_mongo_lags()
        {
            var projection = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projections = new IProjection[] { projection };

            // First "run": dispatch a checkpoint but never flush to MongoDB (simulates a crash
            // between two timed flushes). The durable local store keeps the value.
            var firstRun = CreateTracker(60);
            firstRun.SetUp(projections, 1, false);
            await firstRun.UpdateSlotAndSetCheckpointAsync("default", new[] { "Projection" }, 500, true).ConfigureAwait(false);

            var mongoBeforeRestart = _checkPoints.FindOneById("Projection");
            Assert.That(mongoBeforeRestart.Current ?? 0, Is.EqualTo(0), "MongoDB should still be behind (no flush happened)");

            // Second "run": a brand new tracker with a store pointing at the SAME directory
            // reconciles the durable value with the (lagging) MongoDB value, keeping the greater one.
            var secondRun = new ConcurrentCheckpointTracker(
                _checkPoints,
                60,
                new FileSystemCheckpointDurableStore(_storeDir));
            _trackersToDispose.Add(secondRun);
            secondRun.SetUp(projections, 1, false);

            Assert.That(secondRun.GetCheckpoint(projection), Is.EqualTo(500), "restart must recover the dispatched checkpoint from the durable store");

            // The resume point exposed to the projection engine must reflect the recovered value.
            var fullCheckpoint = secondRun.GetFullCheckpoint(projection);
            Assert.That(fullCheckpoint.Current, Is.EqualTo(500));
        }

        [Test]
        public async Task In_memory_status_check_reflects_dispatched_checkpoint_without_a_flush()
        {
            //Two projections in two different slots.
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var projection3 = new Projection3(Substitute.For<ICollectionWrapper<SampleReadModel3, String>>());
            var projections = new IProjection[] { projection1, projection3 };

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);

            //Advance only one slot: the in-memory checker must know the other is still behind, even
            //though nothing has been flushed to MongoDB yet.
            await _concurrentCheckpointTrackerSut.UpdateSlotAndSetCheckpointAsync(
                projection1.Info.SlotName, new[] { projection1.Info.CommonName }, 100, true).ConfigureAwait(false);

            Assert.That(_concurrentCheckpointTrackerSut.IsCheckpointProjectedByAllProjection(100), Is.False);

            //A cross-process (MongoDB) checker cannot even see the first slot yet, because no flush happened.
            var mongoChecker = new MongoDirectConcurrentCheckpointStatusChecker(_db);
            Assert.That(mongoChecker.IsCheckpointProjectedByAllProjection(100), Is.False,
                "MongoDB must still lag until a flush happens");

            //Advance the second slot too, still without any flush.
            await _concurrentCheckpointTrackerSut.UpdateSlotAndSetCheckpointAsync(
                projection3.Info.SlotName, new[] { projection3.Info.CommonName }, 100, true).ConfigureAwait(false);

            //All tracked projections have now dispatched up to 100 in memory.
            Assert.That(_concurrentCheckpointTrackerSut.IsCheckpointProjectedByAllProjection(100), Is.True);
            Assert.That(_concurrentCheckpointTrackerSut.IsCheckpointProjectedByAllProjection(101), Is.False);
        }

        [Test]
        public void Store_folder_seed_is_created_in_mongo_stable_and_inert()
        {
            //Public constructor path: the durable-store folder is derived from a per-database seed
            //persisted in the checkpoints collection. -1 disables the auto-flush timer.
            var firstRun = new ConcurrentCheckpointTracker(_db, -1);
            _trackersToDispose.Add(firstRun);

            var seedDoc = _checkPoints.FindOneById(ConcurrentCheckpointTracker.CheckpointStoreSeedId);
            Assert.That(seedDoc, Is.Not.Null, "the seed document must be created");
            Assert.That(seedDoc.Signature, Is.Not.Null.And.Not.Empty, "the seed must carry a folder name");
            Assert.That(seedDoc.Active, Is.False, "the seed must be inactive so status/slot scans ignore it");
            Assert.That(seedDoc.Slot, Is.Null, "the seed must have no slot");
            _seedFoldersToDelete.Add(GetSeedFolder(seedDoc.Signature));

            //A restart against the SAME database must resolve the same seed (same folder).
            var secondRun = new ConcurrentCheckpointTracker(_db, -1);
            _trackersToDispose.Add(secondRun);
            var seedDoc2 = _checkPoints.FindOneById(ConcurrentCheckpointTracker.CheckpointStoreSeedId);
            Assert.That(seedDoc2.Signature, Is.EqualTo(seedDoc.Signature), "the seed must be stable across restarts");

            //The seed document must not be seen as a projection nor create a phantom slot.
            var projection = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());
            var slotStatus = new SlotStatusManager(_db, new[] { projection.Info });
            var status = slotStatus.GetSlotsStatus();
            Assert.That(status.AllSlots, Is.EquivalentTo(new[] { projection.Info.SlotName }),
                "the seed document must not create a phantom slot");
        }

        [Test]
        public void Verify_projection_change_info_for_projection_that_changed_signature()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());

            var projections = new IProjection[] { projection1 };
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature + "modified")
            {
                Slot = projection1.Info.SlotName,
            };

            _checkPoints.InsertMany(new[] { checkpoint1, });

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetProjectionChangeInfo();
            var singleProjectionStatus = status.Single();
            Assert.That((Int32)(singleProjectionStatus.ChangeType | ProjectionChangeInfo.ProjectionChangeType.SignatureChange), Is.GreaterThan(0));
            Assert.That(singleProjectionStatus.OldSignature, Is.EqualTo(projection1.Info.Signature + "modified"));
            Assert.That(singleProjectionStatus.ActualSignature, Is.EqualTo(projection1.Info.Signature));
        }

        [Test]
        public void Verify_projection_change_info_for_projection_that_changed_slot()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());

            var projections = new IProjection[] { projection1 };
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature)
            {
                Slot = projection1.Info.SlotName + "modified"
            };

            _checkPoints.InsertMany(new[] { checkpoint1, });

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetProjectionChangeInfo();
            var singleProjectionStatus = status.Single();
            Assert.That((Int32)(singleProjectionStatus.ChangeType | ProjectionChangeInfo.ProjectionChangeType.SlotChange), Is.GreaterThan(0));
            Assert.That(singleProjectionStatus.OldSlot, Is.EqualTo(projection1.Info.SlotName + "modified"));
            Assert.That(singleProjectionStatus.ActualSlot, Is.EqualTo(projection1.Info.SlotName));
        }

        [Test]
        public void Verify_projection_change_info_for_projection_that_changed_slot_and_signature()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());

            var projections = new IProjection[] { projection1 };
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature + "modified")
            {
                Slot = projection1.Info.SlotName + "modified"
            };

            _checkPoints.InsertMany(new[] { checkpoint1, });

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetProjectionChangeInfo();
            var singleProjectionStatus = status.Single();
            Assert.That((Int32)(singleProjectionStatus.ChangeType | ProjectionChangeInfo.ProjectionChangeType.SlotChange), Is.GreaterThan(0));
            Assert.That((Int32)(singleProjectionStatus.ChangeType | ProjectionChangeInfo.ProjectionChangeType.SignatureChange), Is.GreaterThan(0));

            Assert.That(singleProjectionStatus.OldSlot, Is.EqualTo(projection1.Info.SlotName + "modified"));
            Assert.That(singleProjectionStatus.ActualSlot, Is.EqualTo(projection1.Info.SlotName));
            Assert.That(singleProjectionStatus.OldSignature, Is.EqualTo(projection1.Info.Signature + "modified"));
            Assert.That(singleProjectionStatus.ActualSignature, Is.EqualTo(projection1.Info.Signature));
        }

        [Test]
        public void Verify_slot_status_error()
        {
            var projections = SetupTwoProjectionsError();

            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();

            Assert.That(status.SlotsThatNeedsRebuild, Has.Count.EqualTo(1));
        }

        [Test]
        public void Verify_creation_slot_at_value_should_have_current_to_the_same_value()
        {
            var p1 = SetupNewSlot();
            int currentCheckpoint = 50;
            _slotStatusCheckerSut.CreateSlotAtCheckpoint(p1.Info.SlotName, currentCheckpoint);
            var status = _slotStatusCheckerSut.GetSlotsStatus();
            var p1Checkpoint = _checkPoints.AsQueryable().FirstOrDefault(p => p.Id == p1.Info.CommonName);
            Assert.That(p1Checkpoint, Is.Not.Null);
            Assert.That(p1Checkpoint.Value, Is.EqualTo(currentCheckpoint), $"Value should be {currentCheckpoint}");
            Assert.That(p1Checkpoint.Current, Is.EqualTo(currentCheckpoint), $"Current should be {currentCheckpoint}");
        }

        private IProjection SetupNewSlot()
        {
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel3, string>(storageFactory, new NotifyToNobody());
            var p1 = new Projection3(writer1);

            var projections = new IProjection[] { p1 };
            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());

            return p1;
        }

        private void SetupOneProjectionNew()
        {
            _concurrentCheckpointTrackerSut = CreateTracker(60);
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel, string>(storageFactory, new NotifyToNobody());
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(storageFactory, new NotifyToNobody());

            var projection1 = new Projection(writer1);
            var projection2 = new Projection2(writer2);
            var projections = new IProjection[] { projection1, projection2 };
            var p1 = new Checkpoint(projection1.Info.CommonName, 42, projection1.Info.Signature);
            p1.Current = p1.Value;
            p1.Slot = projection1.Info.SlotName;
            _checkPoints.Save(p1, p1.Id);

            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
        }

        /// <summary>
        /// If a rebuild starts then stops, we have a particular situation where signature is ok, current is null
        /// and if the slot is not marked as to rebuild, the engine will simply restart the slot redispatching everything.
        /// </summary>
        private void SetupOneProjectionWithCurrentNull()
        {
            _concurrentCheckpointTrackerSut = CreateTracker(60);
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel, string>(storageFactory, new NotifyToNobody());

            var projection1 = new Projection(writer1);
            var projections = new IProjection[] { projection1 };
            var p1 = new Checkpoint(projection1.Info.CommonName, 42, projection1.Info.Signature);
            p1.Slot = projection1.Info.SlotName;
            p1.Value = 42;
            p1.Current = null;
            _checkPoints.Save(p1, p1.Id);

            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
        }

        private void SetupOneProjectionChangedSignature()
        {
            _concurrentCheckpointTrackerSut = CreateTracker(60);
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel, string>(storageFactory, new NotifyToNobody());
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(storageFactory, new NotifyToNobody());

            var projection1 = new Projection(writer1);
            var projection2 = new Projection2(writer2);
            var projections = new IProjection[] { projection1, projection2 };
            var p1 = new Checkpoint(projection1.Info.CommonName, 42, projection1.Info.Signature);
            p1.Slot = projection1.Info.SlotName;
            p1.Current = p1.Value;
            _checkPoints.Save(p1, p1.Id);
            var p2 = new Checkpoint(projection2.Info.CommonName, 42, "oldSignature");
            p2.Slot = projection2.Info.SlotName;
            p2.Current = p2.Value;
            _checkPoints.Save(p2, p2.Id);

            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
        }

        private IProjection[] SetupTwoProjectionsError()
        {
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel, string>(storageFactory, new NotifyToNobody());
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(storageFactory, new NotifyToNobody());

            var projection1 = new Projection(writer1);
            var projection2 = new Projection2(writer2);
            var projections = new IProjection[] { projection1, projection2 };
            var p1 = new Checkpoint(projection1.Info.CommonName, 42, projection1.Info.Signature);
            p1.Current = p1.Value;
            p1.Slot = projection1.Info.SlotName;
            _checkPoints.Save(p1, p1.Id);

            var p2 = new Checkpoint(projection2.Info.CommonName, 40, projection2.Info.Signature);
            p2.Slot = projection1.Info.SlotName;
            p2.Current = p2.Value;
            _checkPoints.Save(p2, p2.Id);

            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());

            _concurrentCheckpointTrackerSut = CreateTracker(60);
            _concurrentCheckpointTrackerSut.SetUp(projections, 1, false);
            return projections;
        }
    }
}

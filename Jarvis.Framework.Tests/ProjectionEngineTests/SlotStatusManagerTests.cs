using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;
using NSubstitute;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    /// <summary>
    /// Verify <see cref="SlotStatusManager"/> classe
    /// </summary>
    [TestFixture]
    public class SlotStatusManagerTests
    {
        private IMongoDatabase _db;
        private SlotStatusManager _slotStatusCheckerSut;
        private IMongoCollection<Checkpoint> _checkPoints;

        [SetUp]
        public void SetUp()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _checkPoints = _db.GetCollection<Checkpoint>("checkpoints");
            _checkPoints.Drop();
            _slotStatusCheckerSut = null;
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
            _checkPoints.InsertMany(new[] { checkpoint1, checkpoint2, checkpoint3 });

            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();

            Assert.That(status.NewSlots, Has.Count.EqualTo(0));
            Assert.That(status.SlotsThatNeedsRebuild, Has.Count.EqualTo(0));
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
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature) { Slot = projection1.Info.SlotName};
            var checkpoint2 = new Checkpoint(projection2.Info.CommonName, 1, projection2.Info.Signature) { Slot = projection2.Info.SlotName};
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

            _checkPoints.InsertMany(new[] { checkpoint1, checkpoint2, });

            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();

            Assert.That(status.NewSlots, Is.EquivalentTo(new[] { projection3.Info.SlotName}));

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

            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetSlotsStatus();
            Assert.That(status.SlotsThatNeedsRebuild, Has.Count.EqualTo(1));
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

            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            var status = _slotStatusCheckerSut.GetProjectionChangeInfo();
            var singleProjectionStatus = status.Single();
            Assert.That((Int32) (singleProjectionStatus.ChangeType | ProjectionChangeInfo.ProjectionChangeType.SignatureChange), Is.GreaterThan(0));
            Assert.That(singleProjectionStatus.OldSignature, Is.EqualTo(projection1.Info.Signature + "modified"));
            Assert.That(singleProjectionStatus.ActualSignature, Is.EqualTo(projection1.Info.Signature));
        }

        [Test]
        public void Verify_projection_change_info_for_projection_that_changed_slot()
        {
            //Two projection in the same slot
            var projection1 = new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>());

            var projections = new IProjection[] { projection1 };
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature) {
                Slot = projection1.Info.SlotName + "modified"
            };

            _checkPoints.InsertMany(new[] { checkpoint1, });

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
            var checkpoint1 = new Checkpoint(projection1.Info.CommonName, 1, projection1.Info.Signature+ "modified")
            {
                Slot = projection1.Info.SlotName + "modified"
            };

            _checkPoints.InsertMany(new[] { checkpoint1, });

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

        private void SetupOneProjectionNew()
        {
            var concurrentCheckpointTrackerSut = new ConcurrentCheckpointTracker(_db);
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel, string>(storageFactory, new NotifyToNobody());
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(storageFactory, new NotifyToNobody());

            var projection1 = new Projection(writer1);
            var projection2 = new Projection2(writer2);
            var projections = new IProjection[] { projection1, projection2 };
            var p1 = new Checkpoint(projection1.Info.CommonName, 42, projection1.Info.Signature);
            p1.Slot = projection1.Info.SlotName;
            _checkPoints.Save(p1, p1.Id);

            concurrentCheckpointTrackerSut.SetUp(projections, 1, false);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
        }

        private void SetupOneProjectionChangedSignature()
        {
            var concurrentCheckpointTrackerSut = new ConcurrentCheckpointTracker(_db);
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel, string>(storageFactory, new NotifyToNobody());
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(storageFactory, new NotifyToNobody());

            var projection1 = new Projection(writer1);
            var projection2 = new Projection2(writer2);
            var projections = new IProjection[] { projection1, projection2 };
            var p1 = new Checkpoint(projection1.Info.CommonName, 42, projection1.Info.Signature);
            p1.Slot = projection1.Info.SlotName;
            _checkPoints.Save(p1, p1.Id);
            var p2 = new Checkpoint(projection2.Info.CommonName, 42, "oldSignature");
            p2.Slot = projection2.Info.SlotName;
            _checkPoints.Save(p2, p2.Id);

            concurrentCheckpointTrackerSut.SetUp(projections, 1, false);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
        }

        private IProjection[] SetupTwoProjectionsError()
        {
            var concurrentCheckpointTrackerSut = new ConcurrentCheckpointTracker(_db);
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel, string>(storageFactory, new NotifyToNobody());
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(storageFactory, new NotifyToNobody());

            var projection1 = new Projection(writer1);
            var projection2 = new Projection2(writer2);
            var projections = new IProjection[] { projection1, projection2 };
            var p1 = new Checkpoint(projection1.Info.CommonName, 42, projection1.Info.Signature);
            p1.Slot = projection1.Info.SlotName;
            _checkPoints.Save(p1, p1.Id);

            var p2 = new Checkpoint(projection2.Info.CommonName, 40, projection2.Info.Signature);
            p2.Slot = projection1.Info.SlotName;
            _checkPoints.Save(p2, p2.Id);

            concurrentCheckpointTrackerSut.SetUp(projections, 1, false);
            _slotStatusCheckerSut = new SlotStatusManager(_db, projections.Select(p => p.Info).ToArray());
            return projections;
        }
    }
}

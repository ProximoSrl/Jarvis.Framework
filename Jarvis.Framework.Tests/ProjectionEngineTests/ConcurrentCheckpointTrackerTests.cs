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

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class ConcurrentCheckpointTrackerTests
    {
        IMongoDatabase _db;
        ConcurrentCheckpointTracker _sut;
        IMongoCollection<Checkpoint> _checkPoints;
        [SetUp]
        public void SetUp()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            var sut = new ConcurrentCheckpointTracker(_db);

            _checkPoints = _db.GetCollection<Checkpoint>("checkpoints");
            _checkPoints.Drop();
        }

        [Test]
        public void Verify_check_of_new_projection()
        {
            SetupOneProjectionNew();
            RebuildSettings.Init(false, false);
            var errors = _sut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Is.EqualTo("Error in slot default: we have new projections at checkpoint 0.\n REBUILD NEEDED!"));
        }

        [Test]
        public void Verify_check_of_signature_change()
        {
            RebuildSettings.Init(false, false);
            SetupOneProjectionChangedSignature();
            var errors = _sut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Is.EqualTo("Projection Projection2 [slot default] has signature V2 but checkpoint on database has signature oldSignature.\n REBUILD NEEDED"));
        }

        [Test]
        public void Verify_check_of_signature_change_during_rebuild()
        {
            RebuildSettings.Init(true, true);
            SetupOneProjectionChangedSignature();
            var errors = _sut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(0));
        }


        [Test]
        public void Verify_check_of_generic_projection_checkpoint_error()
        {
            RebuildSettings.Init(false, false);
            SetupTwoProjectionsError();
            var errors = _sut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Is.EqualTo("Error in slot default, not all projection at the same checkpoint value. Please check reamodel db!"));
        }

        [Test]
        public void Verify_check_of_new_projection_ok_when_rebuild()
        {
            SetupOneProjectionNew();
            RebuildSettings.Init(true, false);
            var errors = _sut.GetCheckpointErrors();
            Assert.That(errors, Has.Count.EqualTo(0));
        }

        private void SetupOneProjectionNew()
        {
            _sut = new ConcurrentCheckpointTracker(_db);
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel, string>(storageFactory, new NotifyToNobody());
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(storageFactory, new NotifyToNobody());

            var projection1 = new Projection(writer1);
            var projection2 = new Projection2(writer2);
            var projections = new IProjection[] { projection1, projection2 };
            var p1 = new Checkpoint(projection1.GetCommonName(), "42", projection1.GetSignature());
            p1.Slot = projection1.GetSlotName();
            _checkPoints.Save(p1, p1.Id);

            _sut.SetUp(projections, 1);
        }

        private void SetupOneProjectionChangedSignature()
        {
            _sut = new ConcurrentCheckpointTracker(_db);
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel, string>(storageFactory, new NotifyToNobody());
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(storageFactory, new NotifyToNobody());

            var projection1 = new Projection(writer1);
            var projection2 = new Projection2(writer2);
            var projections = new IProjection[] { projection1, projection2 };
            var p1 = new Checkpoint(projection1.GetCommonName(), "42", projection1.GetSignature());
            p1.Slot = projection1.GetSlotName();
            _checkPoints.Save(p1, p1.Id);
            var p2 = new Checkpoint(projection2.GetCommonName(), "42", "oldSignature");
            p2.Slot = projection2.GetSlotName();
            _checkPoints.Save(p2, p2.Id);

            _sut.SetUp(projections, 1);
        }

        private void SetupTwoProjectionsError()
        {
            _sut = new ConcurrentCheckpointTracker(_db);
            var rebuildContext = new RebuildContext(false);
            var storageFactory = new MongoStorageFactory(_db, rebuildContext);
            var writer1 = new CollectionWrapper<SampleReadModel, string>(storageFactory, new NotifyToNobody());
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(storageFactory, new NotifyToNobody());

            var projection1 = new Projection(writer1);
            var projection2 = new Projection2(writer2);
            var projections = new IProjection[] { projection1, projection2 };
            var p1 = new Checkpoint(projection1.GetCommonName(), "42", projection1.GetSignature());
            p1.Slot = projection1.GetSlotName();
            _checkPoints.Save(p1, p1.Id);

            var p2 = new Checkpoint(projection2.GetCommonName(), "40", projection2.GetSignature());
            p2.Slot = projection1.GetSlotName();
            _checkPoints.Save(p2, p2.Id);

            _sut.SetUp(projections, 1);
        }
    }
}

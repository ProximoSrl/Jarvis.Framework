using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class ConcurrentCheckpointTrackerTests
    {
        private ConcurrentCheckpointTracker _sut;
        private IMongoDatabase _db;
        private IMongoCollection<Checkpoint> _checkpointsCollection;

        [SetUp]
        public void SetUp()
        {
            _db = TestHelper.GetTestDb();
            _sut = new ConcurrentCheckpointTracker(_db);
            _checkpointsCollection = _db.GetCollection<Checkpoint>(ConcurrentCheckpointTracker.CollectionName);
        }

        [TearDown]
        public void TearDown()
        {
            _db.Drop();
        }

        [Test]
        public async Task Verify_basic_flush_on_db()
        {
            _sut.SetUp(new[] { GetProjection("test", "testslot") }, 1, true);
            await _sut.UpdateSlotAndSetCheckpointAsync("testslot", new[] { "test" }, 1).ConfigureAwait(false);

            //verify we have nothing flush on db.
            var checkpoint = _checkpointsCollection.FindOneById("test");
            Assert.That(checkpoint.Value, Is.EqualTo(0));
            Assert.That(checkpoint.Current, Is.Null);

            //flush and then verify db
            await _sut.FlushCheckpointCollectionAsync().ConfigureAwait(false);
            checkpoint = _checkpointsCollection.FindOneById("test");
            Assert.That(checkpoint.Value, Is.EqualTo(1));
            Assert.That(checkpoint.Current, Is.EqualTo(1));
        }

        [Test]
        public async Task Verify_flush_on_db_on_multiple_slots()
        {
            _sut.SetUp(new[] { GetProjection("test1", "testslot1") }, 1, true);
            _sut.SetUp(new[] { GetProjection("test2", "testslot2") }, 1, true);
            await _sut.UpdateSlotAndSetCheckpointAsync("testslot1", new[] { "test1" }, 1).ConfigureAwait(false);
            await _sut.UpdateSlotAndSetCheckpointAsync("testslot2", new[] { "test2" }, 2).ConfigureAwait(false);

            //flush and then verify db
            await _sut.FlushCheckpointCollectionAsync().ConfigureAwait(false);
            var checkpoint = _checkpointsCollection.FindOneById("test1");
            Assert.That(checkpoint.Value, Is.EqualTo(1));
            Assert.That(checkpoint.Current, Is.EqualTo(1));

            checkpoint = _checkpointsCollection.FindOneById("test2");
            Assert.That(checkpoint.Value, Is.EqualTo(2));
            Assert.That(checkpoint.Current, Is.EqualTo(2));
        }

        [Test]
        public async Task Verify_flush_multiple_projections()
        {
            //Two projection in the same slot, but in this situation we flush the entire
            //slot with the very same value.
            _sut.SetUp(new[] { GetProjection("test1", "testslot") }, 1, true);
            _sut.SetUp(new[] { GetProjection("test2", "testslot") }, 1, true);
            await _sut.UpdateSlotAndSetCheckpointAsync("testslot", new[] { "test1", "test2" }, 2).ConfigureAwait(false);

            //flush and then verify db
            await _sut.FlushCheckpointCollectionAsync().ConfigureAwait(false);
            var checkpoint = _checkpointsCollection.FindOneById("test1");
            Assert.That(checkpoint.Value, Is.EqualTo(2));
            Assert.That(checkpoint.Current, Is.EqualTo(2));

            checkpoint = _checkpointsCollection.FindOneById("test2");
            Assert.That(checkpoint.Value, Is.EqualTo(2));
            Assert.That(checkpoint.Current, Is.EqualTo(2));
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

        private IProjection GetProjection(string name, string slotName)
        {
            var projection = Substitute.For<IProjection>();
            projection.Info.Returns(new ProjectionInfoAttribute(slotName, "v1", name));
            return projection;
        }
    }
}

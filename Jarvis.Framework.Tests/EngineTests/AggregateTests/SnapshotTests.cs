namespace Jarvis.Framework.Tests.EngineTests.AggregateTests
{
    [TestFixture]
    public class SnapshotTests
    {
        private AggregateTestSampleAggregate1 sut;
        private ISnapshottable snapshottable;
        private IEventSourcedAggregate sourcedAggregate;

        [SetUp]
        public void SetUp()
        {
            GenerateSut();
        }

        private void GenerateSut(Boolean initialize = true)
        {
            snapshottable = sut = new AggregateTestSampleAggregate1();
            sourcedAggregate = sut;
            if (initialize)
            {
                sut.Init("AggregateTestSampleAggregate1_42");
            }
        }

        [Test]
        public void Verify_basic_snapshot_get_from_aggregate()
        {
            sut.Touch();
            string newSignature = Guid.NewGuid().ToString();
            sut.InternalState.SetSignature(newSignature);
            ApplyChanges();

            var snapshot = snapshottable.GetSnapshot();
            Assert.That(snapshot.SourceId, Is.EqualTo(sut.Id));
            Assert.That(snapshot.SchemaVersion, Is.EqualTo(newSignature));
            Assert.That(snapshot.SourceVersion, Is.EqualTo(1));
            Assert.That(snapshot.Payload, Is.InstanceOf<AggregateTestSampleAggregate1State>());
            var state = (AggregateTestSampleAggregate1State)snapshot.Payload;

            Assert.That(state.TouchCount, Is.EqualTo(1));
        }

        [Test]
        public void Verify_basic_snapshot_get_snapshot_from_inner_entities()
        {
            sut.Touch();
            sut.SampleEntity.AddValue(19);
            string newSignature = Guid.NewGuid().ToString();
            sut.SampleEntity.InternalState.SetSignature(newSignature);
            ApplyChanges();

            var snapshot = snapshottable.GetSnapshot();

            Assert.That(snapshot.SourceId, Is.EqualTo(sut.Id));
            Assert.That(snapshot.SourceVersion, Is.EqualTo(1));
            Assert.That(snapshot.Payload, Is.InstanceOf<AggregateTestSampleAggregate1State>());
            var state = (AggregateTestSampleAggregate1State)snapshot.Payload;

            Assert.That(state.EntityStates.Count, Is.EqualTo(1));
            var entityState = state.EntityStates[sut.SampleEntity.Id];
            Assert.That(entityState, Is.InstanceOf<AggregateTestSampleEntityState>());
            AggregateTestSampleEntityState entityRealState = (AggregateTestSampleEntityState)entityState;
            Assert.That(entityRealState.Accumulator, Is.EqualTo(19));
            Assert.That(entityRealState.VersionSignature, Is.EqualTo(newSignature));
        }

        [Test]
        public void Verify_snapshot_restore()
        {
            sut.Touch();
            ApplyChanges();
            var snapshot = snapshottable.GetSnapshot();
            var originalState = sut.InternalState;

            GenerateSut(false); //this will discard old aggregate and create another
            var snapshotRestored = snapshottable.TryRestore(snapshot);

            Assert.That(snapshotRestored);
            Assert.That(sut.InternalState.TouchCount, Is.EqualTo(1));
            Assert.IsFalse(Object.ReferenceEquals(sut.InternalState, originalState), "Restored state is the same instance of payload state");
            Assert.That(sut.Version, Is.EqualTo(1));
            Assert.That(sut.Id, Is.EqualTo("AggregateTestSampleAggregate1_42"));
        }

        [Test]
        public void Verify_snapshot_restore_with_child_entities()
        {
            sut.Touch();
            sut.SampleEntity.AddValue(19);
            ApplyChanges();
            var snapshot = snapshottable.GetSnapshot();
            var originalState = sut.SampleEntity.InternalState;

            GenerateSut(false); //this will discard old aggregate and create another
            var snapshotRestored = snapshottable.TryRestore(snapshot);

            Assert.That(snapshotRestored);
            Assert.IsFalse(Object.ReferenceEquals(sut.SampleEntity.InternalState, originalState), "Restored state is the same instance of payload state for child entity.");
            var entityState = sut.SampleEntity.InternalState;
            Assert.That(entityState.Accumulator, Is.EqualTo(19));
        }

        [Test]
        public void Verify_snapshot_restore_with_child_entities_then_modification_then_snapshot_again()
        {
            sut.Touch();
            sut.SampleEntity.AddValue(19);
            ApplyChanges();
            var snapshot = snapshottable.GetSnapshot();

            GenerateSut(false); //this will discard old aggregate and create another
            var snapshotRestored = snapshottable.TryRestore(snapshot);
            Assert.That(snapshotRestored);
            sut.SampleEntity.AddValue(19);

            var entityState = sut.SampleEntity.InternalState;
            Assert.That(entityState.Accumulator, Is.EqualTo(38));

            ApplyChanges();
            snapshot = snapshottable.GetSnapshot();

            GenerateSut(false); //this will discard old aggregate and create another
            snapshotRestored = snapshottable.TryRestore(snapshot);
            Assert.That(snapshotRestored);
            Assert.That(entityState.Accumulator, Is.EqualTo(38));
        }

        [Test]
        public void Verify_snapshot_restore_check_state_version()
        {
            sut.Touch();
            sut.InternalState.SetSignature(Guid.NewGuid().ToString());
            ApplyChanges();
            var snapshot = snapshottable.GetSnapshot();

            GenerateSut(false); //this will discard old aggregate and create another
            var snapshotRestored = snapshottable.TryRestore(snapshot);

            Assert.That(snapshotRestored, Is.False);
            Assert.That(sut.InternalState, Is.Null);
            Assert.That(sut.Version, Is.EqualTo(0));
            Assert.That(sut.Id, Is.Null);
        }

        [Test]
        public void Verify_snapshot_restore_check_state_version_for_inner_entities()
        {
            sut.Touch();
            sut.SampleEntity.AddValue(19);
            string newSignature = Guid.NewGuid().ToString();
            sut.SampleEntity.InternalState.SetSignature(newSignature);
            ApplyChanges();

            var snapshot = snapshottable.GetSnapshot();

            GenerateSut(false); //this will discard old aggregate and create another
            var snapshotRestored = snapshottable.TryRestore(snapshot);

            Assert.That(snapshotRestored, Is.False);
            Assert.That(sut.InternalState, Is.Null);
            Assert.That(sut.Version, Is.EqualTo(0));
            Assert.That(sut.Id, Is.Null);
        }

        [Test]
        public void Verify_serialization_of_user_state()
        {
            AggregateTestSampleAggregate1 aggregate = new AggregateTestSampleAggregate1();
            aggregate.Init(new AggregateTestSampleAggregate1Id(1));
            aggregate.Touch();

            var snapshot = ((ISnapshottable)aggregate).GetSnapshot();

            var serialized = snapshot.ToJson();
            Assert.That(!serialized.Contains(" \"_t\" : \"AggregateTestSampleAggregate1Id\""));
        }

        private void ApplyChanges()
        {
            var changeset = sourcedAggregate.GetChangeSet();
            sourcedAggregate.Persisted(changeset);
        }
    }
}

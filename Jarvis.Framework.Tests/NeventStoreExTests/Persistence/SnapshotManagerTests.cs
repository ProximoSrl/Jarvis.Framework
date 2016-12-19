using Jarvis.Framework.Tests.EngineTests;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using NUnit.Framework;
using System;
using NSubstitute;
using Jarvis.Framework.TestHelpers;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using Jarvis.Framework.Kernel.Engine;
using NEventStore;

namespace Jarvis.Framework.Tests.NeventStoreExTests.Persistence
{
    [TestFixture]
    public class SnapshotManagerTests
    {
        CachedSnapshotManager _sut;
        ISnapshotPersister _persister;
        private ISnapshotPersistenceStrategy _snapshotPersistenceStrategy;
        Int32 _counter = 0;
        private const Int32 NumberOfCommitsBeforeSnapshot = 50;

        [SetUp]
        public void SetUp()
        {
            _persister = NSubstitute.Substitute.For<ISnapshotPersister>();
            _snapshotPersistenceStrategy = new NumberOfCommitsShapshotPersistenceStrategy(NumberOfCommitsBeforeSnapshot);
            _sut = new CachedSnapshotManager(_persister, _snapshotPersistenceStrategy);
        }

        private String GetAggregateId()
        {
            return "SampleAggregate_" + _counter++;
        }

        [Test]
        public void verify_basic_call_to_persister()
        {
            var aggregateId = GetAggregateId();
            _sut.Load(aggregateId, Int32.MaxValue, typeof(SampleAggregate));
            _persister.Received().Load(aggregateId, Int32.MaxValue, typeof(SampleAggregate).FullName);
        }

        [Test]
        public void verify_snapshot_on_new_aggregate_not_call_persistence()
        {
            var sampleAggregateId = new SampleAggregateId(_counter++);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregate.SampleAggregateState(), sampleAggregateId);
            aggregate.Create();
            _sut.Snapshot(aggregate, "Jarvis", 1);
            _persister.DidNotReceiveWithAnyArgs().Persist(null, null);
        }

        [Test]
        public void verify_persistence_called_after_threshold_new_aggregate()
        {
            var sampleAggregateId = new SampleAggregateId(_counter++);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregate.SampleAggregateState(), sampleAggregateId);
            aggregate.Create();
            _sut.Snapshot(aggregate, "Jarvis", 1);
            _persister.DidNotReceiveWithAnyArgs().Persist(null, null);

            for (int i = 0; i < NumberOfCommitsBeforeSnapshot; i++)
            {
                aggregate.Touch();
            }
            _sut.Snapshot(aggregate, "Jarvis", NumberOfCommitsBeforeSnapshot);
            //should be persisted because it has more events than snapshotreshold
            _persister.ReceivedWithAnyArgs().Persist(null, aggregate.GetType().FullName);
        }

        [Test]
        public void verify_persistence_honor_database_version_when_threshold_passed()
        {
            var sampleAggregateId = new SampleAggregateId(_counter++);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregate.SampleAggregateState(), sampleAggregateId);
            var memento = new AggregateSnapshot<SampleAggregate.SampleAggregateState>() { Id = sampleAggregateId, Version = 30, State = new SampleAggregate.SampleAggregateState() };
            var snapshot = new Snapshot("Jarvis", sampleAggregateId, 30, memento);

            _persister.Load(null, 0, null).ReturnsForAnyArgs(snapshot);

            //Simulate the flow of a repository, first operation load the snapshot
            _sut.Load(sampleAggregateId, Int32.MaxValue, typeof(SampleAggregate));

            //then the repository restored the snapshot
            ((ISnapshotable)aggregate).Restore(memento);

            //now iterate
            _persister.DidNotReceiveWithAnyArgs().Persist(null, null);

            for (int i = 0; i < NumberOfCommitsBeforeSnapshot; i++)
            {
                aggregate.Touch();
            }
            _sut.Snapshot(aggregate, "Jarvis", NumberOfCommitsBeforeSnapshot);
            //should be persisted, becauase it is loaded at version 30, but then another snapshotthreshold count of event were raised
            _persister.ReceivedWithAnyArgs().Persist(null, aggregate.GetType().FullName);
        }   
    }
}

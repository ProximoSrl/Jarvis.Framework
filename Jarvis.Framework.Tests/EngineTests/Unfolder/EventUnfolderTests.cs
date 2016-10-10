using Castle.Core.Logging;
using Castle.MicroKernel;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.ProjectionEngine.Unfolder;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using MongoDB.Driver;
using NEventStore;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.EngineTests.Unfolder
{
    [TestFixture]
    public class EventUnfolderTests
    {
        TestProjector sut;

        [SetUp]
        public void SetUp()
        {
            sut = new TestProjector();
        }

        [Test]
        public void Basic_apply_event_on_unfolder()
        {
            sut.ApplyEvent(new TestDomainEvent() { Value = 42 });
            Assert.That(sut.GetProjection().Sum, Is.EqualTo(42));
        }

        [Test]
        public void Basic_memento_restore()
        {
            sut.ApplyEvent(new TestDomainEvent() { Value = 42 });
            var memento = sut.GetSnapshot();

            var newSut1 = new TestProjector();
            newSut1.Restore(memento);

            var newSut2 = new TestProjector();
            newSut2.Restore(memento);

            newSut1.ApplyEvent(new TestDomainEvent() { Value = 1 });
            Assert.That(newSut1.GetProjection().Sum, Is.EqualTo(43));

            newSut2.ApplyEvent(new TestDomainEvent() { Value = 2 });
            Assert.That(newSut2.GetProjection().Sum, Is.EqualTo(44), "State shared instances between events");
        }

    }

    [TestFixture]
    public class InMemoryAggregateReadModelTest
    {
        TestAggregateQueryModel sut;

        [SetUp]
        public void SetUp()
        {
            sut = new TestAggregateQueryModel();
        }


        [Test]
        public void verify_deep_cloning_base_capabilities()
        {
            sut.ListOfInt.Add(1);
            var cloned = (TestAggregateQueryModel)sut.Clone();
            cloned.ListOfInt.Add(2);

            Assert.That(sut.ListOfInt, Is.EquivalentTo(new[] { 1 }));
            Assert.That(cloned.ListOfInt, Is.EquivalentTo(new[] { 1, 2 }));
        }

    }

    [TestFixture]
    public class EventUnfolderRepositoryTests
    {
        QueryModelRepository sut;

        private IMongoDatabase _db;
        private IStoreEvents _eventStore;

        private IdentityManager _identityConverter;
        private AggregateFactory _aggregateFactory = new AggregateFactory(null);
        ProjectorFactory _factory;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            client.DropDatabase(url.DatabaseName);

            _identityConverter = new IdentityManager(new InMemoryCounterService());
            _identityConverter.RegisterIdentitiesFromAssembly(GetType().Assembly);
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            _eventStore = new EventStoreFactory(loggerFactory).BuildEventStore(connectionString);

            _factory = new ProjectorFactory(Substitute.For<IKernel>());
        }

        ISnapshotPersister _persister;

        [SetUp]
        public void SetUp()
        {
            _persister = Substitute.For<ISnapshotPersister>();
            sut = new QueryModelRepository(_factory, _eventStore, _persister);
        }

        Int64 progressive = 0;

        [Test]
        public void verify_basic_in_memory_projection()
        {
            var id = new TestEventUnfolderId(Interlocked.Increment(ref progressive));
            using (var stream = _eventStore.OpenStream("BLAH", id.AsString(), 0, Int32.MaxValue))
            {
                CommitOneValue(stream, 42);
            }

            var unfolder = sut.GetById<TestProjector, TestAggregateQueryModel>(id, Int32.MaxValue);
            var rm = unfolder.GetProjection();
            Assert.That(rm.Sum, Is.EqualTo(42));
            Assert.That(rm.ListOfInt, Is.EquivalentTo(new[] { 42 }));
        }


        [Test]
        public void verify_snapshot_usage()
        {
            var id = new TestEventUnfolderId(Interlocked.Increment(ref progressive));
            using (var stream = _eventStore.OpenStream("BLAH", id.AsString(), 0, Int32.MaxValue))
            {
                CommitOneValue(stream, 1);
                CommitOneValue(stream, 2);
            }

            var unfolder = sut.GetById<TestProjector, TestAggregateQueryModel>(id, 1);
            var memento = unfolder.GetSnapshot();
            Snapshot s = new Snapshot(id.AsString(), 2, memento);

            using (var stream = _eventStore.OpenStream("BLAH", id.AsString(), 0, Int32.MaxValue))
            {
                CommitOneValue(stream, 3);
            }
            _persister.Load(null, 1, null).ReturnsForAnyArgs(s);

            unfolder = sut.GetById<TestProjector, TestAggregateQueryModel>(id, Int32.MaxValue);
            var rm = unfolder.GetProjection();
            Assert.That(rm.Sum, Is.EqualTo(6));
            Assert.That(rm.ListOfInt, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void verify_use_snapshot_strategy()
        {
            var id = new TestEventUnfolderId(Interlocked.Increment(ref progressive));
            using (var stream = _eventStore.OpenStream(id.AsString()))
            {
                CommitOneValue(stream, 42);
            }
            TestProjector.ShouldSnapshotReturn = true;
            sut.GetById<TestProjector, TestAggregateQueryModel>(id, Int32.MaxValue);
            TestProjector.ShouldSnapshotReturn = false;
            _persister.Received().Persist(Arg.Any<ISnapshot>(), Arg.Any<String>());
        }

        [Test]
        public void verify_projection_up_to_certain_event()
        {
            var id = new TestEventUnfolderId(Interlocked.Increment(ref progressive));
            using (var stream = _eventStore.OpenStream("BLAH", id.AsString(), 0, Int32.MaxValue))
            {
                CommitOneValue(stream, 1);
                CommitOneValue(stream, 2);
                CommitOneValue(stream, 3);
            }

            var unfolder = sut.GetById<TestProjector, TestAggregateQueryModel>(id, 1);
            var rm = unfolder.GetProjection();
            Assert.That(rm.Sum, Is.EqualTo(1));
            Assert.That(rm.ListOfInt, Is.EquivalentTo(new[] { 1 }));

            unfolder = sut.GetById<TestProjector, TestAggregateQueryModel>(id, 2);
            rm = unfolder.GetProjection();
            Assert.That(rm.Sum, Is.EqualTo(3));
            Assert.That(rm.ListOfInt, Is.EquivalentTo(new[] { 1, 2 }));

            unfolder = sut.GetById<TestProjector, TestAggregateQueryModel>(id, 3);
            rm = unfolder.GetProjection();
            Assert.That(rm.Sum, Is.EqualTo(6));
            Assert.That(rm.ListOfInt, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        private static void CommitOneValue(IEventStream stream, int value)
        {
            stream.Add(new EventMessage()
            {
                Body = new TestDomainEvent() { Value = value }
            });
            stream.CommitChanges(Guid.NewGuid());
        }
    }

    [TestFixture]
    public class EventUnfolderRepositoryTestsWithFakeStoreEvents
    {
        QueryModelRepository sut;

        private IMongoDatabase _db;
        private IStoreEvents _eventStore;

        private IdentityManager _identityConverter;
        private AggregateFactory _aggregateFactory = new AggregateFactory(null);
        ProjectorFactory _factory;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            client.DropDatabase(url.DatabaseName);

            _identityConverter = new IdentityManager(new InMemoryCounterService());
            _identityConverter.RegisterIdentitiesFromAssembly(GetType().Assembly);
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            _eventStore = Substitute.For<IStoreEvents>();

            _factory = new ProjectorFactory(Substitute.For<IKernel>());
        }

        ISnapshotPersister _persister;

        [SetUp]
        public void SetUp()
        {
            _persister = Substitute.For<ISnapshotPersister>();
            sut = new QueryModelRepository(_factory, _eventStore, _persister);
        }

     
        [Test]
        public void verify_snapshot_check_version()
        {
            var id = new TestEventUnfolderId(1);
            var unfolder = new TestProjector();
            var memento = (EventUnfolderMemento) unfolder.GetSnapshot();
            memento.Signature = "OTHER"; //change the version of the memento.
            Snapshot s = new Snapshot(id, 2, memento);
            _persister.Load("", 1, "").ReturnsForAnyArgs(s);
            sut.GetById<TestProjector, TestAggregateQueryModel>(id, 200);
            _eventStore.Received().OpenStream("BLAH", id, 0, 200);
        }

        [Test]
        public void verify_snapshot_wrong_version_gets_deleted()
        {
            var id = new TestEventUnfolderId(1);
            var unfolder = new TestProjector();
            var memento = (EventUnfolderMemento)unfolder.GetSnapshot();
            memento.Signature = "OTHER"; //change the version of the memento.
            Snapshot s = new Snapshot(id, 2, memento);
            _persister.Load("", 1, "").ReturnsForAnyArgs(s);
            sut.GetById<TestProjector, TestAggregateQueryModel>(id, 200);
            _persister.Received().Clear(id, memento.Version, typeof(TestProjector).Name);
        }
    }

    [Serializable]
    public class TestAggregateQueryModel : BaseAggregateQueryModel
    {
        public Int32 Sum { get; set; }

        public List<Int32> ListOfInt { get; set; }

        public TestAggregateQueryModel()
        {
            Sum = 0;
            ListOfInt = new List<int>();
        }

        protected void When(TestDomainEvent evt)
        {
            Sum += evt.Value;
            ListOfInt.Add(evt.Value);
        }
    }

    public class TestEventUnfolderId : EventStoreIdentity
    {
        public TestEventUnfolderId(long id) : base(id)
        {
        }

        public TestEventUnfolderId(string id) : base(id)
        {
        }
    }

    public class TestProjector : Projector<TestAggregateQueryModel>
    {
        public static Boolean ShouldSnapshotReturn { get; set; }

        public TestProjector() : base()
        {

        }

        public override string Signature
        {
            get
            {
                return "TEST";
            }
        }

        public override string BucketId
        {
            get
            {
                return "BLAH";
            }
        }

        public override bool ShouldSnapshot(int numOfEventsLoaded)
        {
            return ShouldSnapshotReturn;
        }
    }


    public class TestDomainEvent : DomainEvent
    {
        public Int32 Value { get; set; }
    }
}

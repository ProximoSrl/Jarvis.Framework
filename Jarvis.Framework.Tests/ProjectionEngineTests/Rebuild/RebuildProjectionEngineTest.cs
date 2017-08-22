using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using MongoDB.Driver;
using NEventStore;
using NUnit.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Tests.EngineTests;
using NSubstitute;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using NEventStore.Domain.Core;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Kernel.ProjectionEngine.Rebuild;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.Shared.Messages;
using System.Threading;
using Jarvis.Framework.Kernel.Support;
using System.Reflection;
using MongoDB.Bson;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.Rebuild
{
    [TestFixture]
    public abstract class RebuildProjectionEngineBaseTests
    {
        private string _eventStoreConnectionString;
        private IdentityManager _identityConverter;
        protected RepositoryEx Repository;
        protected IStoreEvents _eventStore;
        protected IMongoDatabase _db;
        protected IMongoStorageFactory _storageFactory;

        protected IMongoCollection<UnwindedDomainEvent> _unwindedEventCollection;
        protected IMongoCollection<Checkpoint> _checkpointCollection;

        protected EventUnwinder _eventUnwinder;

        protected RebuildProjectionEngine sut;

        protected MongoReader<SampleReadModel, string> _reader1;
        protected MongoReader<SampleReadModel2, string> _reader2;
        protected MongoReader<SampleReadModel3, string> _reader3;

        protected ConcurrentCheckpointTracker _tracker;

        [SetUp]
        public virtual void TestFixtureSetUp()
        {
            _eventStoreConnectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;

            var url = new MongoUrl(_eventStoreConnectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _db.Drop();

            ProjectionEngineConfig config = new ProjectionEngineConfig();
            config.EventStoreConnectionString = _eventStoreConnectionString;
            config.Slots = new string[] { "*" };
            config.TenantId = new TenantId("A");
            config.BucketInfo = new List<BucketInfo>();
            config.BucketInfo.Add(new BucketInfo() { Slots = new[] { "*" }, BufferSize = 10000 });

            _identityConverter = new IdentityManager(new CounterService(_db));
            _identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);
            ConfigureEventStore();

            _eventUnwinder = new EventUnwinder(config, new TestLogger(LoggerLevel.Info));

            _unwindedEventCollection = _db.GetCollection<UnwindedDomainEvent>("UnwindedEvents");
            MongoFlatMapper.EnableFlatMapping(true);
            MongoFlatIdSerializerHelper.Initialize(_identityConverter);

            var rebuildContext = new RebuildContext(NitroEnabled);
            _storageFactory = new MongoStorageFactory(_db, rebuildContext);

            _reader1 = new MongoReader<SampleReadModel, string>(_db);
            _reader2 = new MongoReader<SampleReadModel2, string>(_db);
            _reader3 = new MongoReader<SampleReadModel3, string>(_db);

            //now configure RebuildProjectionEngine
            _tracker = new ConcurrentCheckpointTracker(_db);

            var projections = BuildProjections().ToArray();

            _tracker.SetUp(projections, 1, false);
            ProjectionEventInspector inspector = new ProjectionEventInspector();
            inspector.AddAssembly(Assembly.GetExecutingAssembly());
            sut = new RebuildProjectionEngine(_eventUnwinder, _tracker, projections, rebuildContext, config, inspector);
            sut.Logger = new TestLogger(LoggerLevel.Debug);

            _checkpointCollection = _db.GetCollection<Checkpoint>("checkpoints");
        }

        [TearDown]
        public virtual void TestFixtureTearDown()
        {
            _eventStore.Dispose();
        }

        protected void ConfigureEventStore()
        {
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            var factory = new EventStoreFactory(loggerFactory);
            _eventStore = factory.BuildEventStore(_eventStoreConnectionString);
            Repository = new RepositoryEx(
                _eventStore,
                new AggregateFactory(),
                new ConflictDetector(),
                _identityConverter,
               NSubstitute.Substitute.For<NEventStore.Logging.ILog>()
           );
        }

        protected abstract IEnumerable<IProjection> BuildProjections();

        protected void WaitForFinish(RebuildStatus status, Int32 timeoutInSeconds = 5)
        {
            DateTime startWait = DateTime.Now;
            while (status.BucketActiveCount > 0 && DateTime.Now.Subtract(startWait).TotalSeconds < timeoutInSeconds)
            {
                Thread.Sleep(100);
            }
            Assert.That(status.BucketActiveCount, Is.EqualTo(0), "Rebuild stuck");
        }

        protected void CreateAggregate(Int64 id = 1)
        {
            var aggregateId = new SampleAggregateId(id);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(aggregateId);
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });
        }

        protected virtual Boolean NitroEnabled { get { return false; } }
    }

    [TestFixture]
    public class Basic_functionality_tests : RebuildProjectionEngineBaseTests
    {
        CollectionWrapper<SampleReadModel, string> _writer;

        protected override IEnumerable<IProjection> BuildProjections()
        {
            _writer = new CollectionWrapper<SampleReadModel, string>(_storageFactory, new NotifyToNobody());
            yield return new Projection(_writer);
        }

        [Test]
        public async Task no_rebuild_occours()
        {
            CreateAggregate();

            var status = await sut.RebuildAsync();
            WaitForFinish(status);
            Assert.That(_writer.All.Count(), Is.EqualTo(0), "No event should be loaded because we did not dispatched anything");
        }

        [Test]
        public async Task unwind_is_called_to_unwind_events()
        {
            CreateAggregate();

            var status = await sut.RebuildAsync();
            WaitForFinish(status);
            Assert.That(_eventUnwinder.UnwindedCollection.AsQueryable().Count(), Is.EqualTo(1), "RebuildProjection should call unwind to ensure all events are unwinded");
        }
    }

    [TestFixture]
    public class Basic_dispatching_tests : RebuildProjectionEngineBaseTests
    {
        CollectionWrapper<SampleReadModel, string> _writer;
        IProjection _projection;
        protected override IEnumerable<IProjection> BuildProjections()
        {
            _writer = new CollectionWrapper<SampleReadModel, string>(_storageFactory, new NotifyToNobody());
            yield return _projection = new Projection(_writer);
        }

        [Test]
        public async Task event_is_dispatched()
        {
            CreateAggregate();

            //prepare the tracker
            _tracker.SetCheckpoint(_projection.Info.CommonName, 1); //Set the projection as dispatched

            var status = await sut.RebuildAsync();
            WaitForFinish(status);
            Assert.That(_writer.All.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task dispatch_only_until_maximum_checkpoint_already_dispatched()
        {
            CreateAggregate(1);
            CreateAggregate(2);
            CreateAggregate(3);

            //prepare the tracker
            ConcurrentCheckpointTracker thisTracker = new ConcurrentCheckpointTracker(_db);
            thisTracker.SetUp(new[] { _projection }, 1, false);
            thisTracker.UpdateSlotAndSetCheckpoint(_projection.Info.SlotName, new[] { _projection.Info.CommonName }, 2); //Set the projection as dispatched

            var status = await sut.RebuildAsync();
            WaitForFinish(status);
            Assert.That(_writer.All.Count(), Is.EqualTo(2));
        }
    }

    [TestFixture]
    public class Basic_dispatching_tests_with_multiple_projection : RebuildProjectionEngineBaseTests
    {
        CollectionWrapper<SampleReadModel, string> _writer1;
        CollectionWrapper<SampleReadModel3, string> _writer3;
        IProjection _projection1, _projection3;

        protected override IEnumerable<IProjection> BuildProjections()
        {
            _writer1 = new CollectionWrapper<SampleReadModel, string>(_storageFactory, new NotifyToNobody());
            yield return _projection1 = new Projection(_writer1);
            _writer3 = new CollectionWrapper<SampleReadModel3, string>(_storageFactory, new NotifyToNobody());
            yield return _projection3 = new Projection3(_writer3); //projection3 has a different slot
        }

        [Test]
        public async Task event_is_dispatched()
        {
            CreateAggregate();

            ConcurrentCheckpointTracker thisTracker = new ConcurrentCheckpointTracker(_db);
            thisTracker.SetUp(new[] { _projection1, _projection3 }, 1, false);
            thisTracker.UpdateSlotAndSetCheckpoint(_projection1.Info.SlotName, new[] { _projection1.Info.CommonName }, 1);
            thisTracker.UpdateSlotAndSetCheckpoint(_projection3.Info.SlotName, new[] { _projection3.Info.CommonName }, 1);

            var status = await sut.RebuildAsync();
            WaitForFinish(status);
            Assert.That(_writer1.All.Count(), Is.EqualTo(1));
            Assert.That(_writer3.All.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task max_dispatched_is_handled_per_distinct_slot()
        {
            CreateAggregate(1);
            CreateAggregate(2);
            CreateAggregate(3);

            //prepare the tracker
            ConcurrentCheckpointTracker thisTracker = new ConcurrentCheckpointTracker(_db);
            thisTracker.SetUp(new[] { _projection1, _projection3 }, 1, false);
            thisTracker.UpdateSlotAndSetCheckpoint(_projection1.Info.SlotName, new[] { _projection1.Info.CommonName }, 2);
            thisTracker.UpdateSlotAndSetCheckpoint(_projection3.Info.SlotName, new[] { _projection3.Info.CommonName }, 1);

            var status = await sut.RebuildAsync();
            WaitForFinish(status);
            Assert.That(_writer1.All.Count(), Is.EqualTo(2));
            Assert.That(_writer3.All.Count(), Is.EqualTo(1));

            var allCheckpoints = _checkpointCollection.FindAll().ToList();
            var projection1 = allCheckpoints.Single(c => c.Id == _projection1.Info.CommonName);
            Assert.That(projection1.Current, Is.EqualTo(2));
            Assert.That(projection1.Value, Is.EqualTo(2));

            var projection2 = allCheckpoints.Single(c => c.Id == _projection3.Info.CommonName);
            Assert.That(projection2.Current, Is.EqualTo(1));
            Assert.That(projection2.Value, Is.EqualTo(1));
        }
    }

    [TestFixture]
    public class Correct_handling_of_signature : RebuildProjectionEngineBaseTests
    {
        CollectionWrapper<SampleReadModel, string> _writer1;
        CollectionWrapper<SampleReadModel3, string> _writer3;
        Projection _projection1;
        Projection3 _projection3;

        protected override IEnumerable<IProjection> BuildProjections()
        {
            _writer1 = new CollectionWrapper<SampleReadModel, string>(_storageFactory, new NotifyToNobody());
            yield return _projection1 = new Projection(_writer1);
            _writer3 = new CollectionWrapper<SampleReadModel3, string>(_storageFactory, new NotifyToNobody());
            yield return _projection3 = new Projection3(_writer3); //projection3 has a different slot
        }

        [Test]
        public async Task signature_is_honored()
        {
            CreateAggregate(1);
            CreateAggregate(2);
            CreateAggregate(3);

            ConcurrentCheckpointTracker thisTracker = new ConcurrentCheckpointTracker(_db);
            thisTracker.SetUp(new IProjection[] { _projection1, _projection3 }, 1, false);
            thisTracker.UpdateSlotAndSetCheckpoint(((IProjection)_projection1).Info.SlotName, new[] { ((IProjection)_projection1).Info.CommonName }, 1);
            thisTracker.UpdateSlotAndSetCheckpoint(((IProjection)_projection3).Info.SlotName, new[] { ((IProjection)_projection3).Info.CommonName }, 1);

            //now change signature.
            _projection3.Signature = "Modified";
            var status = await sut.RebuildAsync();
            WaitForFinish(status);
            var checkpoint = _checkpointCollection.AsQueryable().Single(c => c.Id == ((IProjection)_projection3).Info.CommonName);
            Assert.That(checkpoint.Signature, Is.EqualTo(_projection3.Signature));
        }
    }

    [TestFixture]
    public class Rebuild_test_with_nitro : RebuildProjectionEngineBaseTests
    {
        protected override bool NitroEnabled
        {
            get
            {
                return true;
            }
        }

        CollectionWrapper<SampleReadModel, string> _writer1;
        CollectionWrapper<SampleReadModel3, string> _writer3;
        IProjection _projection1, _projection3;

        protected override IEnumerable<IProjection> BuildProjections()
        {
            _writer1 = new CollectionWrapper<SampleReadModel, string>(_storageFactory, new NotifyToNobody());
            yield return _projection1 = new Projection(_writer1);
            _writer3 = new CollectionWrapper<SampleReadModel3, string>(_storageFactory, new NotifyToNobody());
            yield return _projection3 = new Projection3(_writer3); //projection3 has a different slot
        }

        [Test]
        public async Task event_is_dispatched()
        {
            CreateAggregate();

            ConcurrentCheckpointTracker thisTracker = new ConcurrentCheckpointTracker(_db);
            thisTracker.SetUp(new[] { _projection1, _projection3 }, 1, false);
            thisTracker.UpdateSlotAndSetCheckpoint(_projection1.Info.SlotName, new[] { _projection1.Info.CommonName }, 1);
            thisTracker.UpdateSlotAndSetCheckpoint(_projection3.Info.SlotName, new[] { _projection3.Info.CommonName }, 1);

            var status = await sut.RebuildAsync();
            WaitForFinish(status);
            var coll = _db.GetCollection<BsonDocument>("Sample");
            var allRecord = coll.FindAll().ToList();
            Assert.That(allRecord, Has.Count.EqualTo(1));
        }
    }
}


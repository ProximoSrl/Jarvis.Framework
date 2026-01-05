using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.ProjectionEngine.Rebuild;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.Support;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using NStore.Core.Logging;
using NStore.Core.Persistence;
using NStore.Core.Streams;
using NStore.Domain;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.Rebuild
{
    [TestFixture]
    public abstract class RebuildProjectionEngineBaseTests
    {
        private string _eventStoreConnectionString;
        private IdentityManager _identityConverter;
        protected Repository Repository;
        private ProjectionEngineConfig _config;
        protected IPersistence _persistence;
        protected IMongoDatabase _db;
        private RebuildContext _rebuildContext;
        protected IMongoStorageFactory _storageFactory;

        protected IMongoCollection<UnwindedDomainEvent> _unwindedEventCollection;
        protected IMongoCollection<Checkpoint> _checkpointCollection;

        protected EventUnwinder _eventUnwinder;
        private IProjection[] _projections;
        protected RebuildProjectionEngine sut;

        protected MongoReader<SampleReadModel, string> _reader1;
        protected MongoReader<SampleReadModel2, string> _reader2;
        protected MongoReader<SampleReadModel3, string> _reader3;

        protected ConcurrentCheckpointTracker _tracker;
        private ProjectionEventInspector _inspector;

        [SetUp]
        public virtual void TestFixtureSetUp()
        {
            _eventStoreConnectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;

            var url = new MongoUrl(_eventStoreConnectionString);
            var client = new MongoClient(url.CreateMongoClientSettings());
            _db = client.GetDatabase(url.DatabaseName);
            _db.Drop();

            _identityConverter = new IdentityManager(new CounterService(_db));
            _identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);

            _unwindedEventCollection = _db.GetCollection<UnwindedDomainEvent>("UnwindedEvents");
            MongoFlatMapper.EnableFlatMapping(true);
            MongoFlatIdSerializerHelper.Initialize(_identityConverter);

            _rebuildContext = new RebuildContext(NitroEnabled);
            _storageFactory = new MongoStorageFactory(_db, _rebuildContext);

            _reader1 = new MongoReader<SampleReadModel, string>(_db);
            _reader2 = new MongoReader<SampleReadModel2, string>(_db);
            _reader3 = new MongoReader<SampleReadModel3, string>(_db);

            var loggerFactory = Substitute.For<INStoreLoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<String>()).Returns(NStoreNullLogger.Instance);
            var factory = new EventStoreFactoryTest(loggerFactory);
            _persistence = factory.BuildEventStore(_eventStoreConnectionString).Result;
            Repository = new Repository(
                new AggregateFactoryEx(null),
                new StreamsFactory(_persistence)
           );

            _config = new ProjectionEngineConfig();
            _config.EventStoreConnectionString = _eventStoreConnectionString;
            _config.Slots = new string[] { "*" };
            _config.TenantId = new TenantId("A");
            _config.BucketInfo = new List<BucketInfo>
            {
                new BucketInfo() { Slots = new[] { "*" }, BufferSize = 10000 }
            };

            _eventUnwinder = new EventUnwinder(_config, _persistence, new TestLogger(LoggerLevel.Info));

            _projections = BuildProjections().ToArray();

            //now configure RebuildProjectionEngine
            _tracker = new ConcurrentCheckpointTracker(_db, 60);
            _tracker.SetUp(_projections, 1, false);
            _inspector = new ProjectionEventInspector();
            _inspector.AddAssembly(Assembly.GetExecutingAssembly());

            _checkpointCollection = _db.GetCollection<Checkpoint>("checkpoints");
        }

        protected void CreateSut()
        {
            sut = new RebuildProjectionEngine(
                _eventUnwinder,
                _tracker,
                _projections,
                _rebuildContext,
                _config,
                _inspector,
                NullLoggerThreadContextManager.Instance);
            sut.Logger = new TestLogger(LoggerLevel.Debug);
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

        protected async Task CreateAggregate(Int64 id = 1)
        {
            var aggregateId = new SampleAggregateId(id);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(aggregateId).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
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
            await CreateAggregate().ConfigureAwait(false);

            CreateSut();
            var status = await sut.RebuildAsync().ConfigureAwait(false);
            WaitForFinish(status);
            Assert.That(_writer.All.Count(), Is.EqualTo(0), "No event should be loaded because we did not dispatched anything");
        }

        [Test]
        public async Task unwind_is_called_to_unwind_events()
        {
            await CreateAggregate().ConfigureAwait(false);

            CreateSut();
            var status = await sut.RebuildAsync().ConfigureAwait(false);
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
        public async Task dispatch_only_until_maximum_checkpoint_already_dispatched()
        {
            await CreateAggregate(1).ConfigureAwait(false);
            await CreateAggregate(2).ConfigureAwait(false);
            await CreateAggregate(3).ConfigureAwait(false);

            //prepare the tracker
            ConcurrentCheckpointTracker thisTracker = new ConcurrentCheckpointTracker(_db, 60);
            thisTracker.SetUp(new[] { _projection }, 1, false);
            await thisTracker.UpdateSlotAndSetCheckpointAsync(_projection.Info.SlotName, new[] { _projection.Info.CommonName }, 2, true); //Set the projection as dispatched

            CreateSut();
            var status = await sut.RebuildAsync().ConfigureAwait(false);
            WaitForFinish(status);
            Assert.That(_writer.All.Count(), Is.EqualTo(2));
        }
    }

    [TestFixture]
    public class Basic_dispatching_tests_with_multiple_projection : RebuildProjectionEngineBaseTests
    {
        private CollectionWrapper<SampleReadModel, string> _writer1;
        private CollectionWrapper<SampleReadModel3, string> _writer3;
        private IProjection _projection1, _projection3;

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
            await CreateAggregate().ConfigureAwait(false);

            ConcurrentCheckpointTracker thisTracker = new ConcurrentCheckpointTracker(_db, 60);
            thisTracker.SetUp(new[] { _projection1, _projection3 }, 1, false);
            await thisTracker.UpdateSlotAndSetCheckpointAsync(_projection1.Info.SlotName, new[] { _projection1.Info.CommonName }, 1, true);
            await thisTracker.UpdateSlotAndSetCheckpointAsync(_projection3.Info.SlotName, new[] { _projection3.Info.CommonName }, 1, true);

            CreateSut();
            var status = await sut.RebuildAsync().ConfigureAwait(false);
            WaitForFinish(status);
            Assert.That(_writer1.All.Count(), Is.EqualTo(1));
            Assert.That(_writer3.All.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task max_dispatched_is_handled_per_distinct_slot()
        {
            await CreateAggregate(1).ConfigureAwait(false);
            await CreateAggregate(2).ConfigureAwait(false);
            await CreateAggregate(3).ConfigureAwait(false);

            //prepare the tracker
            ConcurrentCheckpointTracker thisTracker = new ConcurrentCheckpointTracker(_db, 60);
            thisTracker.SetUp(new[] { _projection1, _projection3 }, 1, false);
            await thisTracker.UpdateSlotAndSetCheckpointAsync(_projection1.Info.SlotName, new[] { _projection1.Info.CommonName }, 2, true);
            await thisTracker.UpdateSlotAndSetCheckpointAsync(_projection3.Info.SlotName, new[] { _projection3.Info.CommonName }, 1, true);

            CreateSut();
            var status = await sut.RebuildAsync().ConfigureAwait(false);
            WaitForFinish(status);
            Assert.That(_writer1.All.Count(), Is.EqualTo(2));
            Assert.That(_writer3.All.Count(), Is.EqualTo(1));

            var allCheckpoints = _checkpointCollection.Find(_ => true).ToList();
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
        private CollectionWrapper<SampleReadModel, string> _writer1;
        private CollectionWrapper<SampleReadModel3, string> _writer3;
        private Projection _projection1;
        private Projection3 _projection3;

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
            await CreateAggregate(1).ConfigureAwait(false);
            await CreateAggregate(2).ConfigureAwait(false);
            await CreateAggregate(3).ConfigureAwait(false);

            ConcurrentCheckpointTracker thisTracker = new ConcurrentCheckpointTracker(_db, 60);
            thisTracker.SetUp(new IProjection[] { _projection1, _projection3 }, 1, false);
            await thisTracker.UpdateSlotAndSetCheckpointAsync(((IProjection)_projection1).Info.SlotName, new[] { ((IProjection)_projection1).Info.CommonName }, 1, true);
            await thisTracker.UpdateSlotAndSetCheckpointAsync(((IProjection)_projection3).Info.SlotName, new[] { ((IProjection)_projection3).Info.CommonName }, 1, true);

            //now change signature.
            _projection3.Signature = "Modified";
            CreateSut();
            var status = await sut.RebuildAsync().ConfigureAwait(false);
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

        private CollectionWrapper<SampleReadModel, string> _writer1;
        private CollectionWrapper<SampleReadModel3, string> _writer3;
        private IProjection _projection1, _projection3;

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
            await CreateAggregate().ConfigureAwait(false);

            ConcurrentCheckpointTracker thisTracker = new ConcurrentCheckpointTracker(_db, 60);
            thisTracker.SetUp(new[] { _projection1, _projection3 }, 1, false);
            await thisTracker.UpdateSlotAndSetCheckpointAsync(_projection1.Info.SlotName, new[] { _projection1.Info.CommonName }, 1, true);
            await thisTracker.UpdateSlotAndSetCheckpointAsync(_projection3.Info.SlotName, new[] { _projection3.Info.CommonName }, 1, true);

            CreateSut();
            RebuildStatus status = await sut.RebuildAsync().ConfigureAwait(false);
            WaitForFinish(status);
            var coll = _db.GetCollection<BsonDocument>("Sample");
            var allRecord = coll.FindAll().ToList();
            Assert.That(allRecord, Has.Count.EqualTo(1));
        }
    }
}
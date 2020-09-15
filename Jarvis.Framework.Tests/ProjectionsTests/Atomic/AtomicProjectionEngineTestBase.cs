using Castle.Core.Logging;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NStore.Core.InMemory;
using NStore.Core.Logging;
using NStore.Core.Persistence;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using static Jarvis.Framework.Tests.DomainTests.DomainEventIdentityBsonSerializationTests;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    public class AtomicProjectionEngineTestBase
    {
        private const Int32 WaitTimeInSeconds = 5;

        protected IMongoDatabase _db;
        protected IPersistence _persistence;
        protected IdentityManager _identityManager;
        protected IMongoCollection<SimpleTestAtomicReadModel> _collection;
        protected IMongoCollection<SimpleAtomicAggregateReadModel> _collectionForAtomicAggregate;

        protected AtomicProjectionEngine _sut;
        protected IWindsorContainer _container;

        protected void Init()
        {
            var identityConverter = new IdentityManager(new InMemoryCounterService());
            MongoFlatIdSerializerHelper.IdentityConverter = identityConverter;
            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleId).Assembly);

            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _collection = GetCollection<SimpleTestAtomicReadModel>();
            _collectionForAtomicAggregate = GetCollection<SimpleAtomicAggregateReadModel>();

            _persistence = CreatePersistence();
            _db.Drop();

            _identityManager = new IdentityManager(new CounterService(_db));

            GenerateContainer();
            SimpleTestAtomicReadModel.TouchMax = Int32.MaxValue;
        }

        private static IPersistence CreatePersistence()
        {
            return new InMemoryPersistence(e => e.BsonSerializeAndDeserialize());
        }

        protected IMongoCollection<T> GetCollection<T>()
            where T : IAtomicReadModel
        {
            return _db.GetCollection<T>(CollectionNames.GetCollectionName<T>());
        }

        protected TestLogger _loggerInstance;

        protected void GenerateContainer()
        {
            _container?.Dispose(); //if we already registered container, just dispose the old one.
            _container = new WindsorContainer();

            _container.Kernel.Resolver.AddSubResolver(new CollectionResolver(_container.Kernel, true));
            _container.Kernel.Resolver.AddSubResolver(new ArrayResolver(_container.Kernel, true));

            _container.AddFacility<JarvisTypedFactoryFacility>();
            _loggerInstance = new TestLogger("Test", LoggerLevel.Info);
            _container.Register(
                Component
                    .For<ILogger>()
                    .Instance(_loggerInstance),
                Component
                    .For<ILoggerFactory>()
                    .AsFactory(),
                Component
                    .For<INStoreLoggerFactory>()
                    .ImplementedBy<NStoreCastleLoggerFactory>(),
                Component
                    .For<IMongoDatabase>()
                    .Instance(_db),
                Component
                    .For<IPersistence>()
                    .Instance(_persistence),
                Component
                    .For<IIdentityManager, IIdentityConverter>()
                    .Instance(_identityManager),
                 Component
                    .For<ICommitEnhancer>()
                    .ImplementedBy<CommitEnhancer>(),
                 Component
                    .For<AtomicProjectionCheckpointManager>(),
                  Component
                    .For<AtomicProjectionEngine>()
                    .ImplementedBy<AtomicProjectionEngine>(),
                Component
                    .For<ILiveAtomicReadModelProcessor>()
                    .ImplementedBy<LiveAtomicReadModelProcessor>(),
                Component
                    .For<ICommitPollingClient>()
                    .ImplementedBy<CommitPollingClient2>()
                    .LifeStyle.Transient,
                Component
                    .For<IAtomicReadModelFactory>()
                    .ImplementedBy<AtomicReadModelFactory>(),
                Component
                    .For<IAtomicReadmodelProjectorHelperFactory>()
                    .ImplementedBy<AtomicReadmodelProjectorHelperFactory>()
                    .DependsOn(Dependency.OnValue<IWindsorContainer>(_container)), //I Know, this is not optima, we will refactor this to a facility.
                Component
                    .For<ICommitPollingClientFactory>()
                    .AsFactory(),
                Component.For(typeof(AtomicReadmodelProjectorHelper<>))
                   .ImplementedBy(typeof(AtomicReadmodelProjectorHelper<>)),
                Component
                    .For(new Type[]
                    {
                            typeof (IAtomicMongoCollectionWrapper<>),
                            typeof (IAtomicCollectionWrapper<>),
                            typeof (IAtomicCollectionReader<>),
                    })
                    .ImplementedBy(typeof(AtomicMongoCollectionWrapper<>))
                    .Named("AtomicMongoCollectionWrapper"),
                  Component
                    .For<IAtomicCollectionWrapperFactory>()
                    .AsFactory(),
                  Component
                    .For<AtomicReadModelSignatureFixer>()
                    .LifestyleTransient()
                );
        }

        private IDisposable _testLoggerScope;

        [SetUp]
        public virtual void SetUp()
        {
            Init();

            _lastCommit = 1;
            _aggregateVersion = 1;
            _aggregateIdSeed++;
            SimpleTestAtomicReadModel.FakeSignature = 1;
            _testLoggerScope = TestLogger.GlobalEnableStartScope();
        }

        [TearDown]
        public virtual void TearDown()
        {
            _testLoggerScope?.Dispose();
        }

        protected Int64 _lastCommit;

        protected Int64 _aggregateIdSeed = 1;
        protected Int32 _aggregateVersion = 1;

        protected Int64 lastUsedPosition;

        protected Task<Changeset> GenerateCreatedEvent(String issuedBy = null)
        {
            var evt = new SampleAggregateCreated();
            SetBasePropertiesToEvent(evt, issuedBy);
            return ProcessEvent(evt);
        }

        protected Task<Changeset> GenerateTouchedEvent(String issuedBy = null)
        {
            var evt = new SampleAggregateTouched();
            SetBasePropertiesToEvent(evt, issuedBy);
            return ProcessEvent(evt);
        }

        protected Task<Changeset> GenerateInvalidatedEvent()
        {
            var evt = new SampleAggregateInvalidated();
            return ProcessEvent(evt);
        }

        protected Task<Changeset> GenerateAtomicAggregateCreatedEvent()
        {
            var evt = new AtomicAggregateCreated();
            return ProcessEvent(evt, p => new AtomicAggregateId(p));
        }

        protected async Task GenerateEmptyUntil(Int64 positionTo)
        {
            IChunk chunk;
            do
            {
                chunk = await _persistence.AppendAsync("Empty", null).ConfigureAwait(false);
                lastUsedPosition = chunk.Position;
            } while (chunk.Position < positionTo);
        }

        protected Task<Changeset> GenerateSampleAggregateDerived1()
        {
            var evt = new SampleAggregateDerived1();
            return ProcessEvent(evt, p => new SampleAggregateId(p));
        }

        protected async Task<Changeset> ProcessEvent(
            DomainEvent evt,
            Func<Int64, IIdentity> generateId = null)
        {
            generateId = generateId ?? (p => new SampleAggregateId(p));
            evt.SetPropertyValue(d => d.AggregateId, generateId(_aggregateIdSeed));
            Changeset cs = new Changeset(_aggregateVersion++, new Object[] { evt });
            var chunk = await _persistence.AppendAsync(evt.AggregateId, cs).ConfigureAwait(false);
            lastUsedPosition = chunk.Position;

            //now set checkpoint in memory to simplify testing
            evt.SetPropertyValue(d => d.CheckpointToken, chunk.Position);
            return cs;
        }

        protected async Task<Changeset> ProcessEvents(
            DomainEvent[] evts,
            Func<Int64, IIdentity> generateId = null)
        {
            Int64 commitId = ++_lastCommit;

            generateId = generateId ?? (p => new SampleAggregateId(p));
            foreach (var evt in evts)
            {
                evt.SetPropertyValue(d => d.AggregateId, generateId(_aggregateIdSeed));
                evt.SetPropertyValue(d => d.CheckpointToken, commitId);
            }

            Changeset cs = new Changeset(_aggregateVersion++, evts);
            var chunk = await _persistence.AppendAsync(evts[0].AggregateId, cs).ConfigureAwait(false);
            lastUsedPosition = chunk.Position;
            return cs;
        }

        protected AtomicProjectionCheckpointManager GetTrackerAndWaitForChangesetToBeProjected(String readmodelName, Int64? positionToCheck = null)
        {
            var tracker = _container.Resolve<AtomicProjectionCheckpointManager>();
            positionToCheck = positionToCheck ?? lastUsedPosition;
            DateTime startWait = DateTime.UtcNow;
            while (DateTime.UtcNow.Subtract(startWait).TotalSeconds < WaitTimeInSeconds
                && tracker.GetCheckpoint(readmodelName) < positionToCheck)
            {
                Thread.Sleep(100);
            }

            Assert.That(tracker.GetCheckpoint(readmodelName), Is.EqualTo(positionToCheck), "Checkpoint not projected");

            return tracker;
        }

        protected async Task<AtomicProjectionEngine> CreateSutAndStartProjectionEngineAsync(Int32 flushTimeInSeconds = 10, Boolean autostart = true)
        {
            var sut = _container.Resolve<AtomicProjectionEngine>();
            sut.RegisterAtomicReadModel(typeof(SimpleTestAtomicReadModel));
            sut.FlushTimeSpan = TimeSpan.FromSeconds(flushTimeInSeconds);

            if (autostart)
            {
                await sut.StartAsync().ConfigureAwait(false);
            }

            return sut;
        }

        protected virtual void OnConfigureProjectionService(AtomicProjectionEngine sut)
        {
        }

        protected async Task<Changeset> GenerateSomeChangesetsAndReturnLatestsChangeset()
        {
            //Three events all generated in datbase
            await GenerateCreatedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);
            return await GenerateTouchedEvent().ConfigureAwait(false);
        }

        protected static void SetBasePropertiesToEvent(DomainEvent evt, string issuedBy)
        {
            var context = new Dictionary<String, Object>();
            context.Add(MessagesConstants.UserId, issuedBy);

            evt.SetPropertyValue(d => d.Context, context);
            evt.SetPropertyValue(e => e.CommitStamp, DateTime.UtcNow);
        }
    }
}

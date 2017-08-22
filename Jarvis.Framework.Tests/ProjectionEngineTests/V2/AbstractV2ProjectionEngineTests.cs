using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core.Logging;
using NEventStore.Domain.Core;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using MongoDB.Driver;
using NEventStore;
using NEventStore.Persistence;
using NSubstitute;
using NUnit.Framework;
using Jarvis.Framework.Shared.Helpers;
using Castle.Windsor;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Jarvis.Framework.Tests.Support;
using Jarvis.Framework.Tests.EngineTests;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
    public abstract class AbstractV2ProjectionEngineTests
    {
        protected ProjectionEngine Engine;
        string _eventStoreConnectionString;
        IdentityManager _identityConverter;
        protected RepositoryEx Repository;
        protected IStoreEvents _eventStore;
        protected IMongoDatabase Database;
        RebuildContext _rebuildContext;
        protected MongoStorageFactory StorageFactory;
        protected IMongoCollection<Checkpoint> _checkpoints;
        protected ConcurrentCheckpointTracker _tracker;
        protected MongoDirectConcurrentCheckpointStatusChecker _statusChecker;
        protected WindsorContainer _container;

        protected ICommitPollingClientFactory _pollingClientFactory;

        protected AbstractV2ProjectionEngineTests(String pollingClientVersion = "1")
        {
            TestHelper.RegisterSerializerForFlatId<SampleAggregateId>();
            _container = new WindsorContainer();
            _container.AddFacility<TypedFactoryFacility>();
            _container.Register(
                Component
                    .For<ICommitPollingClient>()
                    .ImplementedBy<CommitPollingClient2>()
                    .LifestyleTransient(),
                Component
                    .For<ICommitPollingClientFactory>()
                    .AsFactory(),
                Component
                    .For<ICommitEnhancer>()
                    .UsingFactoryMethod(() => new CommitEnhancer(_identityConverter)),
                Component
                    .For<ILogger>()
                    .Instance(NullLogger.Instance));

            switch (pollingClientVersion)
            {
                case "1":
                    throw new NotSupportedException("Version 1 of projection engine was removed due to migration to NES6");

                case "2":
                    _pollingClientFactory = _container.Resolve<ICommitPollingClientFactory>();
                    break;

                default:
                    throw new NotSupportedException("Version not supported");
            }
        }

        [TestFixtureSetUp]
        public virtual void TestFixtureSetUp()
        {
            _eventStoreConnectionString = GetConnectionString();

            DropDb();
            TestHelper.ClearAllQueue("cqrs.rebus.test", "cqrs.rebus.errors");

            _identityConverter = new IdentityManager(new CounterService(Database));
            RegisterIdentities(_identityConverter);
            CollectionNames.Customize = name => "rm." + name;

            ConfigureEventStore();
            ConfigureProjectionEngine().Wait();
        }

        protected abstract void RegisterIdentities(IdentityManager identityConverter);

        protected abstract string GetConnectionString();

        void DropDb()
        {
            var url = new MongoUrl(_eventStoreConnectionString);
            var client = new MongoClient(url);
            Database = client.GetDatabase(url.DatabaseName);
            _checkpoints = Database.GetCollection<Checkpoint>("checkpoints");
            client.DropDatabase(url.DatabaseName);
        }

        [TestFixtureTearDown]
        public virtual void TestFixtureTearDown()
        {
            Engine.Stop();
            _eventStore.Dispose();
            CollectionNames.Customize = name => name;
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

        protected async Task ConfigureProjectionEngine(Boolean dropCheckpoints = true)
        {
            if (Engine != null)
            {
                Engine.Stop();
            }
            if (dropCheckpoints) _checkpoints.Drop();
            _tracker = new ConcurrentCheckpointTracker(Database);
            _statusChecker = new MongoDirectConcurrentCheckpointStatusChecker(Database);

            var tenantId = new TenantId("engine");

            var config = new ProjectionEngineConfig()
            {
                Slots = new[] { "*" },
                EventStoreConnectionString = _eventStoreConnectionString,
                TenantId = tenantId,
                BucketInfo = new List<BucketInfo>() { new BucketInfo() { Slots = new[] { "*" }, BufferSize = 10 } },
                DelayedStartInMilliseconds = 0,
                ForcedGcSecondsInterval = 0,
                EngineVersion = "v2",
            };

            RebuildSettings.Init(OnShouldRebuild(), OnShouldUseNitro());

            _rebuildContext = new RebuildContext(RebuildSettings.NitroMode);
            StorageFactory = new MongoStorageFactory(Database, _rebuildContext);

            Engine = new ProjectionEngine(
                _pollingClientFactory,
                _tracker,
                BuildProjections().ToArray(),
                new NullHouseKeeper(),
                _rebuildContext,
                new NullNotifyCommitHandled(),
                config
                );
            Engine.LoggerFactory = Substitute.For<ILoggerFactory>();
            Engine.LoggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            await OnStartPolling();
        }

        protected virtual bool OnShouldUseNitro()
        {
            return false;
        }

        protected virtual bool OnShouldRebuild()
        {
            return false;
        }

        protected virtual string OnGetPollingClientId()
        {
            return this.GetType().Name;
        }

        protected virtual Task OnStartPolling()
        {
            return Engine.StartWithManualPollAsync(false);
        }

        protected abstract IEnumerable<IProjection> BuildProjections();
    }
}
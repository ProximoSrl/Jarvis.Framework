using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Jarvis.Framework.Shared.Helpers;
using Castle.Windsor;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Jarvis.Framework.Tests.Support;
using Jarvis.Framework.Tests.EngineTests;
using System.Threading.Tasks;
using Jarvis.Framework.Kernel.Engine;
using System.Threading;
using Jarvis.Framework.Shared.Logging;
using Castle.Facilities.Logging;
using NStore.Domain;
using NStore.Core.Logging;
using NStore.Core.Streams;
using NStore.Core.Persistence;
using NStore.Persistence.Mongo;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
    public abstract class AbstractV2ProjectionEngineTests
    {
        private string _eventStoreConnectionString;
        private IdentityManager _identityConverter;

        protected ProjectionEngine Engine;
        protected Repository Repository;
        protected IPersistence Persistence;
        protected IMongoDatabase Database;

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

            _container.AddFacility<LoggingFacility>(f =>
                f.LogUsing(LoggerImplementation.Null));

            _container.Resolve<ILoggerFactory>();

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
                    .UsingFactoryMethod(() => new CommitEnhancer()),
                Component
                    .For<INStoreLoggerFactory>()
                    .ImplementedBy<NStoreCastleLoggerFactory>(),
                Component
                    .For<ILoggerThreadContextManager>()
                    .ImplementedBy<NullLoggerThreadContextManager>(),
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

        [OneTimeSetUp]
        public virtual void OneTimeSetUp()
        {
            _eventStoreConnectionString = GetConnectionString();

            DropDb();
#if NETFULL
            TestHelper.ClearAllQueue("cqrs.rebus.test", "cqrs.rebus.errors");
#endif
            _identityConverter = new IdentityManager(new CounterService(Database));
            RegisterIdentities(_identityConverter);
#pragma warning disable S2696 // Instance members should not write to "static" fields, this is a trick on test to customize collection names.
            CollectionNames.Customize = name => "rm." + name;
#pragma warning restore S2696 // Instance members should not write to "static" fields

            ConfigureEventStore();
            ConfigureProjectionEngineAsync().Wait();
        }

        protected abstract void RegisterIdentities(IdentityManager identityConverter);

        protected abstract string GetConnectionString();

        private void DropDb()
        {
            var url = new MongoUrl(_eventStoreConnectionString);
            var client = new MongoClient(url);
            Database = client.GetDatabase(url.DatabaseName);
            _checkpoints = Database.GetCollection<Checkpoint>("checkpoints");
            client.DropDatabase(url.DatabaseName);
        }

        [OneTimeTearDown]
        public virtual void OneTimeTearDown()
        {
            Engine.Stop();
#pragma warning disable S2696 // Instance members should not write to "static" fields
            CollectionNames.Customize = name => name;
#pragma warning restore S2696 // Instance members should not write to "static" fields
        }

        protected Task FlushCheckpointCollectionAsync()
        {
            return _tracker.FlushCheckpointCollectionAsync();
        }

        protected void ConfigureEventStore()
        {
            var loggerFactory = Substitute.For<INStoreLoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<String>()).Returns(NStoreNullLogger.Instance);
            var factory = new EventStoreFactoryTest(loggerFactory);
            Persistence = factory.BuildEventStore(_eventStoreConnectionString).Result;
            Repository = new Repository(new AggregateFactoryEx(null), new StreamsFactory(Persistence));
        }

        protected async Task ConfigureProjectionEngineAsync(Boolean dropCheckpoints = true)
        {
            Engine?.Stop();
            if (dropCheckpoints) _checkpoints.Drop();
            _tracker = new ConcurrentCheckpointTracker(Database);
            _statusChecker = new MongoDirectConcurrentCheckpointStatusChecker(Database, _tracker);

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

            var rebuildContext = new RebuildContext(RebuildSettings.NitroMode);
            StorageFactory = new MongoStorageFactory(Database, rebuildContext);

            Engine = new ProjectionEngine(
                _pollingClientFactory,
                Persistence,
                _tracker,
                BuildProjections().ToArray(),
                new NullHouseKeeper(),
                new NullNotifyCommitHandled(),
                config,
                NullLogger.Instance,
                NullLoggerThreadContextManager.Instance);
            Engine.LoggerFactory = Substitute.For<ILoggerFactory>();
            Engine.LoggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            await OnStartPolling().ConfigureAwait(false);
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

        protected Task<Int64> GetLastPositionAsync()
        {
            return Persistence.ReadLastPositionAsync(CancellationToken.None);
        }
    }
}
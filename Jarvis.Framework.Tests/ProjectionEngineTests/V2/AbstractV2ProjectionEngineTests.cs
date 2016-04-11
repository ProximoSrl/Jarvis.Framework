using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core.Logging;
using CommonDomain.Core;
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
        protected ConcurrentCheckpointStatusChecker _statusChecker;

        [TestFixtureSetUp]
        public virtual void TestFixtureSetUp()
        {
            _eventStoreConnectionString = GetConnectionString();

            DropDb();

            _identityConverter = new IdentityManager(new CounterService(Database));
            RegisterIdentities(_identityConverter);
            CollectionNames.Customize = name => "rm." + name;

            ConfigureEventStore();
            ConfigureProjectionEngine();
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
                _identityConverter
                );
        }


        protected void ConfigureProjectionEngine(Boolean dropCheckpoints = true)
        {
            if (Engine != null)
            {
                Engine.Stop();
            }
            if (dropCheckpoints) _checkpoints.Drop();
            _tracker = new ConcurrentCheckpointTracker(Database);
            _statusChecker = new ConcurrentCheckpointStatusChecker(Database);

            var tenantId = new TenantId("engine");

            var config = new ProjectionEngineConfig()
            {
                Slots = new[] { "*" },
                EventStoreConnectionString = _eventStoreConnectionString,
                TenantId = tenantId,
                BucketInfo = new List<BucketInfo>() { new BucketInfo() { Slots = new[] { "*" }, BufferSize = 10 } },
                DelayedStartInMilliseconds = 1000,
                ForcedGcSecondsInterval = 0,
                EngineVersion = "v2",
            };

            RebuildSettings.Init(OnShouldRebuild(), OnShouldUseNitro());

            _rebuildContext = new RebuildContext(RebuildSettings.NitroMode);
            StorageFactory = new MongoStorageFactory(Database, _rebuildContext);
            Func<IPersistStreams, CommitPollingClient> pollingClientFactory = 
                ps => new CommitPollingClient(
                    ps, 
                    new CommitEnhancer(_identityConverter),
                    OnGetPollingClientId(),
                    NullLogger.Instance);
            Engine = new ProjectionEngine(
                pollingClientFactory,
                _tracker,
                BuildProjections().ToArray(),
                new NullHouseKeeper(),
                _rebuildContext,
                new NullNotifyCommitHandled(),
                config
                );
            Engine.LoggerFactory = Substitute.For<ILoggerFactory>();
            Engine.LoggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            OnStartPolling();
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

        protected virtual void OnStartPolling()
        {
            Engine.StartWithManualPoll(false);
        }

        protected abstract IEnumerable<IProjection> BuildProjections();
    }
}
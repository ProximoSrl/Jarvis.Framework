using System;
using System.Configuration;
using System.Diagnostics;
using Castle.Core.Logging;
using NEventStore.Domain.Core;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.TestHelpers;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using MongoDB.Driver;
using NEventStore;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Threading;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using MongoDB.Bson.Serialization;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.NEventStoreEx.Support;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.NEventStoreEx;
using Castle.Windsor;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson;
using Jarvis.Framework.Kernel.Events;
using System.Collections.Generic;

namespace Jarvis.Framework.Tests.Kernel.Commands
{
    [TestFixture]
    public class RepositoryCommandHandlerTests 
    {
        private TouchSampleAggregateHandler _sut;

        private RepositoryEx _repositoryEx;
        private IMongoDatabase _db;
        private IStoreEvents _eventStore;
        private IdentityManager _identityConverter;
        private AggregateFactory _aggregateFactory = new AggregateFactory(null);

		private WindsorContainer _container;

        private IMongoCollection<BsonDocument> _eventsCollection;

        [SetUp]
        public void SetUp()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _eventsCollection = _db.GetCollection<BsonDocument>("Commits");

            client.DropDatabase(url.DatabaseName);

            _identityConverter = new IdentityManager(new InMemoryCounterService());
            _identityConverter.RegisterIdentitiesFromAssembly(GetType().Assembly);
            MongoFlatIdSerializerHelper.Initialize(_identityConverter);
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            _eventStore = new EventStoreFactory(loggerFactory).BuildEventStore(connectionString);
            _repositoryEx = CreateRepository();

			_container = new WindsorContainer();
			_container.AddFacility<TypedFactoryFacility>();
			_container.Register(Component.For<IRepositoryExFactory>().AsFactory());
			_container.Register(Component
				.For<IRepositoryEx>()
				.UsingFactoryMethod(() => CreateRepository())
				.LifestyleTransient());

            IRepositoryExFactory factory = NSubstitute.Substitute.For<IRepositoryExFactory>();
            factory.Create().Returns(_repositoryEx);

            _sut = new TouchSampleAggregateHandler
            {
                Repository = _repositoryEx,
                AggregateFactory = _aggregateFactory,
                AggregateCachedRepositoryFactory = new AggregateCachedRepositoryFactory(factory),
                EventStoreQueryManager = new DirectMongoEventStoreQueryManager(_db),
            };
        }

        private RepositoryEx CreateRepository()
        {
            var repositoryEx = new RepositoryEx(
                _eventStore,
                _aggregateFactory,
                new ConflictDetector(),
                _identityConverter,
                NSubstitute.Substitute.For<NEventStore.Logging.ILog>()
            );
            repositoryEx.SnapshotManager = Substitute.For<ISnapshotManager>();
            repositoryEx.SnapshotManager.Load("", 0, typeof(SampleAggregate)).ReturnsForAnyArgs<ISnapshot>((ISnapshot)null);
            return repositoryEx;
        }

        [TearDown]
        public void TearDown()
        {
            _repositoryEx.Dispose();
        }

        [Test]
        public void command_handler_should_throw_if_modified_since_check_fails()
        {
            SampleAggregate aggregate = CreateAndSaveAggregate();

            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            cmd.SetContextData(MessagesConstants.IfVersionEqualsTo, aggregate.Version.ToString());

            //now simulate another change of the entity
            aggregate.Touch();
            _repositoryEx.Save(aggregate, Guid.NewGuid(), null);

            Assert.Throws<AggregateModifiedException>(() => _sut.Handle(cmd));
        }

        [Test]
        public void command_handler_should_pass_if_modified_since_check_is_ok()
        {
            SampleAggregate aggregate = CreateAndSaveAggregate();

            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            var startVersion = aggregate.Version;
            cmd.SetContextData(MessagesConstants.IfVersionEqualsTo, startVersion.ToString());

            _sut.Handle(cmd);
            Assert.That(_sut.Aggregate.Version, Is.EqualTo(startVersion + 1));
        }

        [Test]
        public void command_handler_should_throw_if_checkpoint_token_not_pass()
        {
            SampleAggregate aggregate = CreateAndSaveAggregate();

            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            var checkpointToken = GetLastCheckpointTokenFromEvenstoreDb();
            String sessionId = Guid.NewGuid().ToString();
            cmd.SetContextData(MessagesConstants.SessionStartCheckpointToken, checkpointToken.ToString());
            cmd.SetContextData(MessagesConstants.OfflineSessionId, sessionId);
            cmd.SetContextData(MessagesConstants.SessionStartCheckEnabled, "true");

            //now simulate another change of the entity
            aggregate.Touch();
            _repositoryEx.Save(aggregate, Guid.NewGuid(), null);

            Assert.Throws<AggregateSyncConflictException>(() => _sut.Handle(cmd));
        }

        [Test]
        public void command_handler_should_not_throw_if_checkpoint_token_not_pass_but_session_check_is_not_enabled()
        {
            SampleAggregate aggregate = CreateAndSaveAggregate();

            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            var checkpointToken = GetLastCheckpointTokenFromEvenstoreDb();
            String sessionId = Guid.NewGuid().ToString();
            cmd.SetContextData(MessagesConstants.SessionStartCheckpointToken, checkpointToken.ToString());
            cmd.SetContextData(MessagesConstants.OfflineSessionId, sessionId);
            
            //DO NOT SET HEADER FOR SESSION START CHECK ENABLED
            //cmd.SetContextData(MessagesConstants.SessionStartCheckEnabled, "true");

            //now simulate another change of the entity
            aggregate.Touch();
            _repositoryEx.Save(aggregate, Guid.NewGuid(), null);

            _sut.Handle(cmd);
        }

        [Test]
        public void command_handler_should_not_throw_if_there_are_newer_commits_but_of_same_session()
        {
            SampleAggregate aggregate = CreateAndSaveAggregate();

            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            var checkpointToken = GetLastCheckpointTokenFromEvenstoreDb();
            String sessionId = Guid.NewGuid().ToString();
            cmd.SetContextData(MessagesConstants.SessionStartCheckpointToken, checkpointToken.ToString());
            cmd.SetContextData(MessagesConstants.OfflineSessionId, sessionId);
            cmd.SetContextData(MessagesConstants.SessionStartCheckEnabled, "true");

            //now simulate another change of the entity
            aggregate.Touch();
            _repositoryEx.Save(aggregate, Guid.NewGuid(),d => 
            {
                d.Add(MessagesConstants.OfflineSessionId, sessionId);
            });

            _sut.Handle(cmd);
        }

        public Int64 GetLastCheckpointTokenFromEvenstoreDb()
        {
           
            var lastCommitSynced = _eventsCollection
              .Find(Builders<BsonDocument>.Filter.Empty)
              .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
              .Project(Builders<BsonDocument>.Projection.Include("_id"))
              .Limit(1)
              .FirstOrDefault();

            return lastCommitSynced == null ? 0 : lastCommitSynced["_id"].AsInt64;
        }

        private SampleAggregate CreateAndSaveAggregate()
        {
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregate.SampleAggregateState(), sampleAggregateId);
            aggregate.Create();
            aggregate.Touch();
            _repositoryEx.Save(aggregate, Guid.NewGuid(), null);
            return aggregate;
        }

    }

}

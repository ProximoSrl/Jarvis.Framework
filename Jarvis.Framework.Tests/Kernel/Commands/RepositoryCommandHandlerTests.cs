using System;
using System.Configuration;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.IdentitySupport;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.Tests.EngineTests;
using Castle.Windsor;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson;
using Jarvis.Framework.Kernel.Events;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Persistence;
using Jarvis.Framework.Shared.Persistence.EventStore;
using NStore.Domain;
using NStore.Core.Persistence;
using NStore.Core.Logging;
using NStore.Core.Streams;

namespace Jarvis.Framework.Tests.Kernel.Commands
{
	[TestFixture]
    public class RepositoryCommandHandlerTests
    {
        private TouchSampleAggregateHandler _sut;

        private Repository _repositoryEx;
        private IPersistence _persistence;
        private readonly AggregateFactoryEx _aggregateFactory = new AggregateFactoryEx(null);

        private IMongoCollection<BsonDocument> _eventsCollection;

        [SetUp]
        public void SetUp()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            var _db = client.GetDatabase(url.DatabaseName);
            _eventsCollection = _db.GetCollection<BsonDocument>(EventStoreFactory.PartitionCollectionName);

            client.DropDatabase(url.DatabaseName);

            var identityConverter = new IdentityManager(new InMemoryCounterService());
            identityConverter.RegisterIdentitiesFromAssembly(GetType().Assembly);
            MongoFlatIdSerializerHelper.Initialize(identityConverter);
            var loggerFactory = Substitute.For<INStoreLoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<String>()).Returns(NStoreNullLogger.Instance);
            _persistence = new EventStoreFactory(loggerFactory)
                .BuildEventStore(connectionString)
                .Result;
            _repositoryEx = CreateRepository();

            var _container = new WindsorContainer();
            _container.AddFacility<TypedFactoryFacility>();
            _container.Register(Component.For<IRepositoryFactory>().AsFactory());
            _container.Register(Component
                .For<IRepository>()
                .UsingFactoryMethod(() => CreateRepository())
                .LifestyleTransient());

            IRepositoryFactory factory = NSubstitute.Substitute.For<IRepositoryFactory>();
            factory.Create().Returns(_repositoryEx);

            _sut = new TouchSampleAggregateHandler
            {
                Repository = _repositoryEx,
                AggregateFactory = _aggregateFactory,
                AggregateCachedRepositoryFactory = new AggregateCachedRepositoryFactory(factory),
                EventStoreQueryManager = new DirectMongoEventStoreQueryManager(_db),
            };
        }

        private Repository CreateRepository()
        {
            return new Repository(
                _aggregateFactory,
                new StreamsFactory(_persistence)
            );
        }

        [Test]
        public async Task command_handler_should_throw_if_modified_since_check_fails()
        {
            SampleAggregate aggregate = await CreateAndSaveAggregate().ConfigureAwait(false);

            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            cmd.SetContextData(MessagesConstants.IfVersionEqualsTo, aggregate.Version.ToString());

            //now simulate another change of the entity
            aggregate.Touch();
            await _repositoryEx.SaveAsync(aggregate, Guid.NewGuid().ToString(), null).ConfigureAwait(false);

            Assert.ThrowsAsync<AggregateModifiedException>(async () => await _sut.HandleAsync(cmd).ConfigureAwait(false));
        }

        [Test]
        public async Task command_handler_should_pass_if_modified_since_check_is_ok()
        {
            SampleAggregate aggregate = await CreateAndSaveAggregate().ConfigureAwait(false);

            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            var startVersion = aggregate.Version;
            cmd.SetContextData(MessagesConstants.IfVersionEqualsTo, startVersion.ToString());

            await _sut.HandleAsync(cmd).ConfigureAwait(false);
            Assert.That(_sut.Aggregate.Version, Is.EqualTo(startVersion + 1));
        }

        [Test]
        public async Task command_handler_should_throw_if_checkpoint_token_not_pass()
        {
            SampleAggregate aggregate = await CreateAndSaveAggregate().ConfigureAwait(false);

            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            var checkpointToken = GetLastCheckpointTokenFromEvenstoreDb();
            String sessionId = Guid.NewGuid().ToString();
            cmd.SetContextData(MessagesConstants.SessionStartCheckpointToken, checkpointToken.ToString());
            cmd.SetContextData(MessagesConstants.OfflineSessionId, sessionId);
            cmd.SetContextData(MessagesConstants.SessionStartCheckEnabled, "true");

            //now simulate another change of the entity
            aggregate.Touch();
            await _repositoryEx.SaveAsync(aggregate, Guid.NewGuid().ToString(), null).ConfigureAwait(false);

            Assert.ThrowsAsync<AggregateSyncConflictException>(async () => await _sut.HandleAsync(cmd).ConfigureAwait(false));
        }

        [Test]
        public async Task command_handler_should_not_throw_if_checkpoint_token_not_pass_but_session_check_is_not_enabled()
        {
            SampleAggregate aggregate = await CreateAndSaveAggregate().ConfigureAwait(false);

            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            var checkpointToken = GetLastCheckpointTokenFromEvenstoreDb();
            String sessionId = Guid.NewGuid().ToString();
            cmd.SetContextData(MessagesConstants.SessionStartCheckpointToken, checkpointToken.ToString());
            cmd.SetContextData(MessagesConstants.OfflineSessionId, sessionId);

            //DO NOT SET HEADER FOR SESSION START CHECK ENABLED
            //cmd.SetContextData(MessagesConstants.SessionStartCheckEnabled, "true");

            //now simulate another change of the entity
            aggregate.Touch();
            await _repositoryEx.SaveAsync(aggregate, Guid.NewGuid().ToString(), null).ConfigureAwait(false);

            await _sut.HandleAsync(cmd).ConfigureAwait(false);
        }

        [Test]
        public async Task command_handler_should_not_throw_if_there_are_newer_commits_but_of_same_session()
        {
            SampleAggregate aggregate = await CreateAndSaveAggregate().ConfigureAwait(false);

            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            var checkpointToken = GetLastCheckpointTokenFromEvenstoreDb();
            String sessionId = Guid.NewGuid().ToString();
            cmd.SetContextData(MessagesConstants.SessionStartCheckpointToken, checkpointToken.ToString());
            cmd.SetContextData(MessagesConstants.OfflineSessionId, sessionId);
            cmd.SetContextData(MessagesConstants.SessionStartCheckEnabled, "true");

            //now simulate another change of the entity
            aggregate.Touch();
            await _repositoryEx.SaveAsync(
                aggregate,
                Guid.NewGuid().ToString(),
                d => d.Add(MessagesConstants.OfflineSessionId, sessionId)).ConfigureAwait(false);

           await _sut.HandleAsync(cmd).ConfigureAwait(false);
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

        private async Task<SampleAggregate> CreateAndSaveAggregate()
        {
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = await _repositoryEx.GetByIdAsync<SampleAggregate>(sampleAggregateId).ConfigureAwait(false);
            aggregate.Create();
            aggregate.Touch();
            await _repositoryEx.SaveAsync(aggregate, Guid.NewGuid().ToString(), null).ConfigureAwait(false);
            return aggregate;
        }
    }
}

using System;
using System.Configuration;
using Castle.Core.Logging;
using CommonDomain.Core;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.TestHelpers;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable InconsistentNaming

namespace Jarvis.Framework.Tests.EngineTests
{
    [TestFixture]
    public class EngineSetupTests
    {
        private MongoServer _server;
        private EventStoreFactory _factory;
        string _connectionString;
        private const string intranetEsTest = "intranet-es-test";

        [SetUp]
        public void SetUp()
        {
            _server = new MongoDB.Driver.MongoClient("mongodb://localhost").GetServer();
            _server.GetDatabase(intranetEsTest).Drop();
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            _factory = new EventStoreFactory(loggerFactory);
            _connectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
        }

        [Test]
        public void setup_with_mongodb()
        {
            _factory.BuildEventStore(_connectionString);

            Assert.IsTrue(_server.DatabaseExists(intranetEsTest));
            Assert.IsTrue(_server.GetDatabase(intranetEsTest).CollectionExists("Commits"));
            Assert.IsTrue(_server.GetDatabase(intranetEsTest).CollectionExists("Streams"));
        }

        [Test]
        public void save_stream()
        {
            var eventStore = _factory.BuildEventStore(_connectionString);
            var repo = new RepositoryEx(
                eventStore,
                new AggregateFactory(),
                new ConflictDetector(),
                new IdentityManager(new InMemoryCounterService())
            );

            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregate.State(), new SampleAggregateId(1));
            aggregate.Create();
            repo.Save(
                aggregate,
                new Guid("{7A63A302-5575-4148-A86D-4421AAFF2019}"),
                null
            );

            var db = _server.GetDatabase(intranetEsTest);
        }

        public ITenant Current { get; private set; }
    }
}

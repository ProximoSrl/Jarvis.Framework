using System;
using System.Configuration;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Jarvis.Framework.Shared.Helpers;
using System.Linq;
using Jarvis.Framework.Kernel.Engine;
using System.Threading.Tasks;
using NStore.Core.Streams;
using NStore.Domain;
using NStore.Core.Logging;

namespace Jarvis.Framework.Tests.EngineTests
{
	[TestFixture]
    public class EngineSetupTests
    {
        private EventStoreFactoryTest _factory;
        private string _connectionString;
        private IMongoDatabase _db;

        [SetUp]
        public void SetUp()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
            this._db = TestHelper.CreateNew(_connectionString);

            var loggerFactory = Substitute.For<INStoreLoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<String>()).Returns(NStoreNullLogger.Instance);
            _factory = new EventStoreFactoryTest(loggerFactory);

            TestHelper.RegisterSerializerForFlatId<SampleAggregateId>();
        }

        [Test]
        public void setup_with_mongodb()
        {
            _factory.BuildEventStore(_connectionString).Wait();

            Assert.IsTrue(_db.Client.ListDatabases().ToList().Any(n => n["name"].AsString == _db.DatabaseNamespace.DatabaseName));
            Assert.IsTrue(_db.ListCollections().ToList().Any(c => c["name"].AsString == EventStoreFactory.PartitionCollectionName));
        }

        [Test]
        public async Task save_stream()
        {
            var eventStore = _factory.BuildEventStore(_connectionString).Result;
            var repository = new Repository(
                new AggregateFactoryEx(null),
                new StreamsFactory(eventStore),
                null
            );

            var aggregate = await repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            await repository.SaveAsync(
                aggregate,
                "{7A63A302-5575-4148-A86D-4421AAFF2019}",
                null
            ).ConfigureAwait(false);
        }
    }
}

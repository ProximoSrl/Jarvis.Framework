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
using Jarvis.Framework.Kernel.ProjectionEngine;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.Rebuild
{
    [TestFixture]
    public class EventUnwinderTests 
    {
      
        string _eventStoreConnectionString;
        IdentityManager _identityConverter;
        protected RepositoryEx Repository;
        protected IStoreEvents _eventStore;
        protected IMongoDatabase _db;

        IMongoCollection<UnwindedDomainEvent> _unwindedEventCollection;

        protected EventUnwinder sut;

        [TestFixtureSetUp]
        public virtual void TestFixtureSetUp()
        {
            _eventStoreConnectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString; ;
            var url = new MongoUrl(_eventStoreConnectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _db.Drop();

            _identityConverter = new IdentityManager(new CounterService(_db));
            _identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);
            ConfigureEventStore();
            ProjectionEngineConfig config = new ProjectionEngineConfig() { EventStoreConnectionString = _eventStoreConnectionString };
            CommitEnhancer commitEnhancer = new CommitEnhancer(_identityConverter);
            sut = new EventUnwinder(config, NullLogger.Instance);

            _unwindedEventCollection = _db.GetCollection<UnwindedDomainEvent>("UnwindedEvents");
            MongoFlatMapper.EnableFlatMapping(true);
            MongoFlatIdSerializerHelper.Initialize(_identityConverter);
        }

        [SetUp]
        public void SetUp()
        {
            _db.Drop();
        }

        [TestFixtureTearDown]
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
                _identityConverter
           );
        }

        protected SampleAggregateId CreateAggregate(Int64 id = 1)
        {
            var aggregateId = new SampleAggregateId(id);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(aggregateId);
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });
            return aggregateId;
        }

        protected SampleAggregateId DoubleTouchAggregate(Int64 id = 1)
        {
            var aggregateId = new SampleAggregateId(id);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(aggregateId);
            aggregate.DoubleTouch();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });
            return aggregateId;
        }

        [Test]
        public void verify_basic_unwinding()
        {
            var id = CreateAggregate();

            sut.Unwind();

            var allEvents = sut.UnwindedCollection.FindAll();
            Assert.That(allEvents.Count(), Is.EqualTo(1));
            var evt = allEvents.Single();
            Assert.That(evt.EventType, Is.EqualTo("SampleAggregateCreated"));
            Assert.That(evt.GetEvent().AggregateId, Is.EqualTo(id));
        }

        [Test]
        public void verify_basic_unwinding_with_headers()
        {
            var aggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(aggregateId);
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { h.Add("test.with.dot", "BLAH"); });

            sut.Unwind();

            var allEvents = sut.UnwindedCollection.FindAll();
            Assert.That(allEvents.Count(), Is.EqualTo(1));
            var evt = allEvents.Single();
            Assert.That(evt.EventType, Is.EqualTo("SampleAggregateCreated")); 
            Assert.That(evt.GetEvent().AggregateId, Is.EqualTo(aggregateId));
            Assert.That(evt.GetEvent().Context["test.with.dot"], Is.EqualTo("BLAH"));
        }


        [Test]
        public void verify_unwinding_preserve_enhancement()
        {
            CreateAggregate();

            sut.Unwind();

            var allEvents = sut.UnwindedCollection.FindAll();
            Assert.That(allEvents.Count(), Is.EqualTo(1));
            var evt = allEvents.Single();
            Assert.That(evt.GetEvent().CheckpointToken, Is.Not.Null);
        }

        [Test]
        public void verify_unwinding_not_miss_events()
        {
            CreateAggregate();
            sut.Unwind();
            DoubleTouchAggregate();
            sut.Unwind();

            var allEvents = sut.UnwindedCollection.FindAll();
            Assert.That(allEvents.Count(), Is.EqualTo(3));
        }
    }

  
}

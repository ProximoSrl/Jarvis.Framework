using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.Support;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.Rebuild
{
    [TestFixture]
    public class EventUnwinderTests
    {
        private string _eventStoreConnectionString;

        protected Repository Repository;
        protected IPersistence _persistence;
        protected IMongoDatabase _db;

        protected EventUnwinder sut;

        [OneTimeSetUp]
        public virtual void TestFixtureSetUp()
        {
            _eventStoreConnectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
            var url = new MongoUrl(_eventStoreConnectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _db.Drop();

            var identityConverter = new IdentityManager(new CounterService(_db));
            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);
            MongoFlatMapper.EnableFlatMapping(true);
            MongoFlatIdSerializerHelper.Initialize(identityConverter);
        }

        [SetUp]
        public void SetUp()
        {
            _db.Drop();
            ConfigureEventStore(); //Creates the _persistence
            Repository = new Repository(
               new AggregateFactoryEx(null),
               new StreamsFactory(_persistence),
               Substitute.For<ISnapshotStore>()
            );

            var config = new ProjectionEngineConfig() { EventStoreConnectionString = _eventStoreConnectionString };
            sut = new EventUnwinder(config, _persistence, NullLogger.Instance);
        }

        protected void ConfigureEventStore()
        {
            var loggerFactory = Substitute.For<INStoreLoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<String>()).Returns(NStoreNullLogger.Instance);
            var factory = new EventStoreFactoryTest(loggerFactory);
            _persistence = factory.BuildEventStore(_eventStoreConnectionString).Result;
        }

        protected async Task<SampleAggregateId> CreateAggregateAsync(Int64 id = 1)
        {
            var aggregateId = new SampleAggregateId(id);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(aggregateId).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
            return aggregateId;
        }

        protected async Task<SampleAggregateId> CreateAggregateAndTouchAsync(Int64 id = 1)
        {
            var aggregateId = new SampleAggregateId(id);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(aggregateId).ConfigureAwait(false);
            aggregate.Create();
            aggregate.Touch();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
            return aggregateId;
        }

        [Test]
        public async Task verify_basic_unwinding()
        {
            var id = await CreateAggregateAsync().ConfigureAwait(false);

            await sut.UnwindAsync().ConfigureAwait(false);

            var allEvents = sut.UnwindedCollection.FindAll();
            Assert.That(allEvents.Count(), Is.EqualTo(1));
            var evt = allEvents.Single();
            Assert.That(evt.EventType, Is.EqualTo("SampleAggregateCreated"));
            Assert.That((evt.GetEvent() as DomainEvent).AggregateId, Is.EqualTo(id));
        }

        [Test]
        public async Task verify_unwinding_of_multiple_events()
        {
            var id = await CreateAggregateAndTouchAsync().ConfigureAwait(false);

            await sut.UnwindAsync().ConfigureAwait(false);

            var allEvents = sut.UnwindedCollection.FindAll().ToList();

            Assert.That(allEvents.Count(), Is.EqualTo(2));
            var evt = allEvents.First();
            Assert.That(evt.EventType, Is.EqualTo("SampleAggregateCreated"));
            Assert.That((evt.GetEvent() as DomainEvent).AggregateId, Is.EqualTo(id));

            evt = allEvents.ElementAt(1);
            Assert.That(evt.EventType, Is.EqualTo("SampleAggregateTouched"));
            Assert.That((evt.GetEvent() as DomainEvent).AggregateId, Is.EqualTo(id));
        }

        [Test]
        public async Task verify_unwind_plain_object()
        {
            var poco = new PocoObject("TEST", 42);
            await _persistence.AppendAsync("poco/42", poco).ConfigureAwait(false);

            await sut.UnwindAsync().ConfigureAwait(false);

            var allEvents = sut.UnwindedCollection.FindAll();
            Assert.That(allEvents.Count(), Is.EqualTo(1));
            var evt = allEvents.Single();
            Assert.That(evt.EventType, Is.EqualTo("PocoObject"));
            Assert.That((evt.GetEvent() as PocoObject).IntValue, Is.EqualTo(42));
            Assert.That((evt.GetEvent() as PocoObject).Value, Is.EqualTo("TEST"));
            Assert.That(evt.PartitionId, Is.EqualTo("poco/42"));
        }

        [Test]
        public async Task verify_basic_unwinding_with_headers()
        {
            var aggregateId = new SampleAggregateId(1);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(aggregateId).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => h.Add("test.with.dot", "BLAH")).ConfigureAwait(false);

            await sut.UnwindAsync().ConfigureAwait(false);

            var allEvents = sut.UnwindedCollection.FindAll();
            Assert.That(allEvents.Count(), Is.EqualTo(1));
            var evt = allEvents.Single();
            Assert.That(evt.EventType, Is.EqualTo("SampleAggregateCreated"));
            Assert.That((evt.GetEvent() as DomainEvent).AggregateId, Is.EqualTo(aggregateId));
            Assert.That((evt.GetEvent() as DomainEvent).Context["test.with.dot"], Is.EqualTo("BLAH"));
        }

        [Test]
        public async Task verify_unwinding_preserve_enhancement()
        {
            await CreateAggregateAsync().ConfigureAwait(false);

            await sut.UnwindAsync().ConfigureAwait(false);

            var allEvents = sut.UnwindedCollection.FindAll();
            Assert.That(allEvents.Count(), Is.EqualTo(1));
            var evt = allEvents.Single();
            Assert.That((evt.GetEvent() as DomainEvent).CheckpointToken, Is.Not.Null);
        }

        [Test]
        public async Task verify_unwinding_not_miss_events()
        {
            var id = await CreateAggregateAsync().ConfigureAwait(false);
            var aggregate = Repository.GetByIdAsync<SampleAggregate>(id).Result;
            aggregate.DoubleTouch();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
            await sut.UnwindAsync().ConfigureAwait(false);

            var allEvents = sut.UnwindedCollection.FindAll();
            Assert.That(allEvents.Count(), Is.EqualTo(3));
        }

        [Test]
        public async Task verify_unwinding_then_modif_then_unwind_again()
        {
            //Save and unwind
            var id = await CreateAggregateAsync().ConfigureAwait(false);
            await sut.UnwindAsync().ConfigureAwait(false);

            //reload, save and unwind.
            var aggregate = Repository.GetByIdAsync<SampleAggregate>(id).Result;
            aggregate.Touch();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
            await sut.UnwindAsync().ConfigureAwait(false);

            var allEvents = sut.UnwindedCollection.FindAll();
            Assert.That(allEvents.Count(), Is.EqualTo(2), "Unwinding in more than one execution missed events.");
        }

        #region Helpers

        public class PocoObject
        {
            public PocoObject(string value, int intValue)
            {
                Value = value;
                IntValue = intValue;
            }

            public String Value { get; set; }

            public Int32 IntValue { get; set; }
        }

        #endregion
    }
}

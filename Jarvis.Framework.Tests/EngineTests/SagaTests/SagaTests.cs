using Jarvis.Framework.Tests.Support;

namespace Jarvis.Framework.Tests.EngineTests.SagaTests
{
    [TestFixture]
    public class SagaTests
    {
        private EventStoreFactoryTest _factory;
        private string _connectionString;
        private WindsorContainer _container;
        private IMongoDatabase _db;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            TestHelper.RegisterSerializerForFlatId<OrderId>();
            _connectionString = ConfigurationManager.ConnectionStrings["saga"].ConnectionString;
            var url = new MongoUrl(_connectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);

            var loggerFactory = Substitute.For<INStoreLoggerFactory>();
            _factory = new EventStoreFactoryTest(loggerFactory);

            _container = new WindsorContainer();
        }

        [SetUp]
        public void Setup()
        {
            _db.Drop();
        }

        [Test]
        public async Task Verify_that_saga_has_correct_number_of_uncommitted_events()
        {
            var orderId = new OrderId(1);
            var sagaId = orderId;

            var eventStore = await _factory.BuildEventStore(_connectionString).ConfigureAwait(false);
            var streamsFactory = new StreamsFactory(eventStore);
            var repo = new Repository(new AggregateFactoryEx(_container.Kernel), streamsFactory, new NullSnapshots());

            var saga = await repo.GetByIdAsync<DeliverPizzaSaga>(sagaId).ConfigureAwait(false);

            saga.MessageReceived(new OrderPlaced(orderId));
            saga.MessageReceived(new BillPrinted(orderId));
            saga.MessageReceived(new PaymentReceived(orderId, Guid.NewGuid()));
            saga.MessageReceived(new PizzaDelivered(orderId));

            //check that uncommitted events are correctly transictioned.
            var events = ((IEventSourcedAggregate)saga).GetChangeSet();

            //Verify that we can save the saga.
            await repo.SaveAsync(saga, Guid.NewGuid().ToString(), null).ConfigureAwait(false);

            Assert.AreEqual(4, events.Events.Length);
        }

        [Test]
        public async Task Verify_that_saga_can_be_reloaded_and_have_no_uncommitted_events()
        {
            var orderId = new OrderId(2);
            var sagaId = orderId;

            var eventStore = await _factory.BuildEventStore(_connectionString).ConfigureAwait(false);
            var streamsFactory = new StreamsFactory(eventStore);
            var repo = new Repository(new AggregateFactoryEx(_container.Kernel), streamsFactory, new NullSnapshots());

            var saga = await repo.GetByIdAsync<DeliverPizzaSaga>(sagaId).ConfigureAwait(false);

            saga.MessageReceived(new OrderPlaced(orderId));
            saga.MessageReceived(new BillPrinted(orderId));
            saga.MessageReceived(new PaymentReceived(orderId, Guid.NewGuid()));
            saga.MessageReceived(new PizzaDelivered(orderId));

            await repo.SaveAsync(saga, Guid.NewGuid().ToString(), null).ConfigureAwait(false);

            var sagaReloaded = await repo.GetByIdAsync<DeliverPizzaSaga>(sagaId).ConfigureAwait(false);

            Assert.That(sagaReloaded, Is.Not.Null);
            var events = ((IEventSourcedAggregate)saga).GetChangeSet();
            Assert.AreEqual(0, events.Events.Length);
        }

        [Test]
        public async Task repository_does_not_serialize_access_to_the_same_entity()
        {
            var orderId = new OrderId(3);
            var sagaId = orderId;

            var eventStore = await _factory.BuildEventStore(_connectionString).ConfigureAwait(false);
            var streamsFactory = new StreamsFactory(eventStore);

            var repo1 = new Repository(new AggregateFactoryEx(_container.Kernel), streamsFactory, new NullSnapshots());
            var repo2 = new Repository(new AggregateFactoryEx(_container.Kernel), streamsFactory, new NullSnapshots());

            var saga1 = await repo1.GetByIdAsync<DeliverPizzaSaga>(sagaId).ConfigureAwait(false);
            saga1.MessageReceived(new OrderPlaced(orderId));

            //now create another thread that loads and change the same entity
            var saga2 = repo2.GetByIdAsync<DeliverPizzaSaga>(sagaId).Result;
            saga2.MessageReceived(new OrderPlaced(orderId));
            repo2.SaveAsync(saga2, Guid.NewGuid().ToString(), null).Wait();

            try
            {
                await repo1.SaveAsync(saga1, Guid.NewGuid().ToString(), null).ConfigureAwait(false);
                Assert.Fail("this is supposed to throw due to concurrency exception");
            }
            catch (Exception)
            {
                //Good we are expecting to throw, 
            }
        }

        [Test]
        public void listener_tests()
        {
            var listener = new DeliverPizzaSagaListener();
            var orderId = new OrderId(4);

            var placed = new OrderPlaced(orderId);
            var printed = new BillPrinted(orderId);
            var received = new PaymentReceived(orderId, Guid.NewGuid());
            var delivered = new PizzaDelivered(orderId);
            var prefix = listener.Prefix;
            Assert.AreEqual(prefix + (string)orderId, listener.GetCorrelationId(placed));
            Assert.AreEqual(prefix + (string)orderId, listener.GetCorrelationId(printed));
            Assert.AreEqual(prefix + (string)orderId, listener.GetCorrelationId(received));
            Assert.AreEqual(prefix + (string)orderId, listener.GetCorrelationId(delivered));
        }

        [Test]
        public void listener_tests_when_id_has_prefix()
        {
            var listener = new DeliverPizzaSagaListener2();
            var orderId = new OrderId(5);

            var placed = new OrderPlaced(orderId);
            var printed = new BillPrinted(orderId);
            var received = new PaymentReceived(orderId, Guid.NewGuid());
            var delivered = new PizzaDelivered(orderId);

            Assert.AreEqual("DeliverPizzaSaga2_" + (string)orderId, listener.GetCorrelationId(placed));
            Assert.AreEqual("DeliverPizzaSaga2_" + (string)orderId, listener.GetCorrelationId(printed));
            Assert.AreEqual("DeliverPizzaSaga2_" + (string)orderId, listener.GetCorrelationId(received));
            Assert.AreEqual("DeliverPizzaSaga2_" + (string)orderId, listener.GetCorrelationId(delivered));
        }
    }

    [TestFixture]
    public class SagaTestsBase
    {
        private EventStoreFactoryTest _factory;
        private string _connectionString;
        private WindsorContainer _container;
        private IMongoDatabase _db;
        private DeliverPizzaSagaListener2 _listener;
        private Repository _repo;
        private IPersistence _persistence;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            TestHelper.RegisterSerializerForFlatId<OrderId>();
            _connectionString = ConfigurationManager.ConnectionStrings["saga"].ConnectionString;
            var url = new MongoUrl(_connectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);

            var loggerFactory = Substitute.For<INStoreLoggerFactory>();
            _factory = new EventStoreFactoryTest(loggerFactory);

            _container = new WindsorContainer();
            _listener = new DeliverPizzaSagaListener2();
        }

        [SetUp]
        public async Task SetUp()
        {
            _db.Drop();
            _persistence = await _factory.BuildEventStore(_connectionString).ConfigureAwait(false);
            var streamsFactory = new StreamsFactory(_persistence);
            _repo = new Repository(new AggregateFactoryEx(_container.Kernel), streamsFactory, new NullSnapshots());
        }

        protected async Task ApplyEventOnSaga(DomainEvent evt)
        {
            var id = _listener.GetCorrelationId(evt);
            var saga = await _repo.GetByIdAsync<DeliverPizzaSaga2>(id).ConfigureAwait(false);
            saga.MessageReceived(evt);
            await _repo.SaveAsync(saga, Guid.NewGuid().ToString(), null).ConfigureAwait(false);
        }

        [Test]
        public async Task Verify_saga_reloaded_status()
        {
            var orderId = new OrderId(100);
            var evt = new OrderPlaced(orderId);

            await ApplyEventOnSaga(evt).ConfigureAwait(false);

            var sagaId = _listener.GetCorrelationId(evt);
            var sagaReloaded = await _repo.GetByIdAsync<DeliverPizzaSaga2>(sagaId).ConfigureAwait(false);
            Assert.That(sagaReloaded.AccessState.PizzaActualStatus, Is.EqualTo("Order Placed"));
            Assert.That(sagaReloaded.Id, Is.EqualTo("DeliverPizzaSaga2_Order_100"));
        }

        [Test]
        public async Task Verify_saga_reloaded_status_after_two_events()
        {
            var orderId = new OrderId(101);
            var evt = new OrderPlaced(orderId);

            await ApplyEventOnSaga(evt).ConfigureAwait(false);

            var evt2 = new BillPrinted(orderId);
            await ApplyEventOnSaga(evt2).ConfigureAwait(false);

            var sagaId = _listener.GetCorrelationId(evt);
            var sagaReloaded = await _repo.GetByIdAsync<DeliverPizzaSaga2>(sagaId).ConfigureAwait(false);
            Assert.That(sagaReloaded.AccessState.PizzaActualStatus, Is.EqualTo("Bill printed"));
        }

        [Test]
        public async Task Verify_saga_does_not_mix_events()
        {
            var orderId = new OrderId(102);
            var evt = new OrderPlaced(orderId);

            await ApplyEventOnSaga(evt).ConfigureAwait(false);

            var orderId2 = new OrderId(103);
            var evt2 = new BillPrinted(orderId2);
            await ApplyEventOnSaga(evt2).ConfigureAwait(false);

            var sagaId = _listener.GetCorrelationId(evt);
            var sagaReloaded = await _repo.GetByIdAsync<DeliverPizzaSaga2>(sagaId).ConfigureAwait(false);
            Assert.That(sagaReloaded.AccessState.PizzaActualStatus, Is.EqualTo("Order Placed"));
        }

        [Test]
        public async Task Verify_emittingCommand()
        {
            var orderId = new OrderId(103);
            var evt = new OrderPlaced(orderId);

            await ApplyEventOnSaga(evt).ConfigureAwait(false);

            var sagaId = _listener.GetCorrelationId(evt);
            var changesets = await _persistence.EventStoreReadChangeset(sagaId).ConfigureAwait(false);
            var changeset = changesets.Single();
            Assert.That(changeset.Events.Length, Is.EqualTo(1));
            var @event = changeset.Events.Single();
            Assert.That(@event, Is.InstanceOf<MessageReaction>(), "We are expecting a messagereaction as payload");
            MessageReaction reaction = (MessageReaction)@event;
            Assert.That(reaction.MessagesOut.Length, Is.EqualTo(1), "We are expeting a messageout when order is placed");
        }

        [Test]
        public async Task Verify_emittingCommand_on_reload()
        {
            var orderId = new OrderId(101);
            var evt = new OrderPlaced(orderId);

            await ApplyEventOnSaga(evt).ConfigureAwait(false);

            var evt2 = new BillPrinted(orderId);
            await ApplyEventOnSaga(evt2).ConfigureAwait(false);

            var sagaId = _listener.GetCorrelationId(evt);

            var changesets = await _persistence.EventStoreReadChangeset(sagaId).ConfigureAwait(false);
            var changeset = changesets.Last();
            Assert.That(changeset.Events.Length, Is.EqualTo(1), "Single event expected");
            var @event = changeset.Events.Single();
            Assert.That(@event, Is.InstanceOf<MessageReaction>(), "We are expecting a messagereaction as payload");
            MessageReaction reaction = (MessageReaction)@event;
            Assert.That(reaction.MessagesOut.Length, Is.EqualTo(0), "Bill printed does not have out events");
        }
    }
}

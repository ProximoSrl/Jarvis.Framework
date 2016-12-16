using System;
using System.Configuration;
using System.Linq;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Tests.Support;
using NEventStore;
using System.Threading.Tasks;
using System.Threading;

namespace Jarvis.Framework.Tests.EngineTests.SagaTests
{
    [TestFixture]
    public class SagaTests
    {
        private EventStoreFactory _factory;
        string _connectionString;

        [SetUp]
        public void SetUp()
        {
            TestHelper.RegisterSerializerForFlatId<OrderId>();

            _connectionString = ConfigurationManager.ConnectionStrings["saga"].ConnectionString;
            var db = TestHelper.CreateNew(_connectionString);

            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            _factory = new EventStoreFactory(loggerFactory);
        }

        [Test]
        public void Verify_that_saga_has_correct_number_of_uncommitted_events()
        {
            var orderId = new OrderId(1);
            var sagaId = orderId;

            var eventStore = _factory.BuildEventStore(_connectionString);
            var repo = new SagaEventStoreRepositoryEx(eventStore, new SagaFactory());

            var saga = repo.GetById<DeliverPizzaSaga>(sagaId);

            saga.Transition(new OrderPlaced(orderId));
            saga.Transition(new BillPrinted(orderId));
            saga.Transition(new PaymentReceived(orderId, Guid.NewGuid()));
            saga.Transition(new PizzaDelivered(orderId));
            //check that uncommitted events are correctly transictioned.
            var events = ((ISagaEx)saga).GetUncommittedEvents().ToArray();

            repo.Save(saga, Guid.NewGuid(), null);

            Assert.AreEqual(4, events.Count());
        }

        [Test]
        public void Verify_that_saga_can_be_reloaded_and_have_no_uncommitted_events()
        {
            var orderId = new OrderId(1);
            var sagaId = orderId;

            var eventStore = _factory.BuildEventStore(_connectionString);
            var repo = new SagaEventStoreRepositoryEx(eventStore, new SagaFactory());

            var saga = repo.GetById<DeliverPizzaSaga>(sagaId);

            saga.Transition(new OrderPlaced(orderId));
            saga.Transition(new BillPrinted(orderId));
            saga.Transition(new PaymentReceived(orderId, Guid.NewGuid()));
            saga.Transition(new PizzaDelivered(orderId));

            repo.Save(saga, Guid.NewGuid(), null);

            var sagaReloaded = repo.GetById<DeliverPizzaSaga>(sagaId);

            Assert.That(sagaReloaded, Is.Not.Null);
            var uncommittedevents = ((ISagaEx)saga).GetUncommittedEvents().ToArray();
            Assert.AreEqual(0, uncommittedevents.Count());
        }

        [Test]
        public void repository_can_serialize_access_to_the_same_entity()
        {
            var orderId = new OrderId(1);
            var sagaId = orderId;

            var eventStore = _factory.BuildEventStore(_connectionString);

            using (var repo1 = new SagaEventStoreRepositoryEx(eventStore, new SagaFactory()))
            using (var repo2 = new SagaEventStoreRepositoryEx(eventStore, new SagaFactory()))
            {
                var saga1 = repo1.GetById<DeliverPizzaSaga>(sagaId);
                saga1.Transition(new OrderPlaced(orderId));

                //now create another thread that loads and change the same entity
                var task = Task<Boolean>.Factory.StartNew(() =>
                {
                    var saga2 = repo2.GetById<DeliverPizzaSaga>(sagaId);
                    saga2.Transition(new OrderPlaced(orderId));
                    repo2.Save(saga2, Guid.NewGuid(), null);
                    return true;
                });

                Thread.Sleep(100); //Let be sure the other task is started doing something.
                repo1.Save(saga1, Guid.NewGuid(), null);
                Assert.IsTrue(task.Result); //inner should not throw.
            }
        }


        [Test]
        public void listener_tests()
        {
            var listener = new DeliverPizzaSagaListener();
            var orderId = new OrderId(1);

            var placed = new OrderPlaced(orderId);
            var printed = new BillPrinted(orderId);
            var received = new PaymentReceived(orderId, Guid.NewGuid());
            var delivered = new PizzaDelivered(orderId);
            var prefix = listener.Prefix;
            Assert.AreEqual(prefix+(string)orderId, listener.GetCorrelationId(placed));
            Assert.AreEqual(prefix + (string)orderId, listener.GetCorrelationId(printed));
            Assert.AreEqual(prefix + (string)orderId, listener.GetCorrelationId(received));
            Assert.AreEqual(prefix + (string)orderId, listener.GetCorrelationId(delivered));
        }


        [Test]
        public void listener_tests_when_id_has_prefix()
        {
            var listener = new DeliverPizzaSagaListener2();
            var orderId = new OrderId(1);

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
        private EventStoreFactory _factory;
        string _connectionString;
        protected IStoreEvents _eventStore;
        SagaEventStoreRepositoryEx _repo;
        DeliverPizzaSagaListener2 _listener;

        [SetUp]
        public void SetUp()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["saga"].ConnectionString;
            var db = TestHelper.CreateNew(_connectionString);

            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            _factory = new EventStoreFactory(loggerFactory);

            _eventStore = _factory.BuildEventStore(_connectionString);
            _repo = new SagaEventStoreRepositoryEx(_eventStore, new SagaFactory());
            _listener =  new DeliverPizzaSagaListener2(); 
        }


        protected void ApplyEventOnSaga(DomainEvent evt)
        {
            var id = _listener.GetCorrelationId(evt);
            var saga = _repo.GetById<DeliverPizzaSaga2>(id);
            saga.Transition(evt);
            _repo.Save(saga, Guid.NewGuid(), null);
        }

        [Test]
        public void Verify_saga_reloaded_status()
        {
            var orderId = new OrderId(42);
            var evt = new OrderPlaced(orderId);

            ApplyEventOnSaga(evt);

            var sagaId = _listener.GetCorrelationId(evt);
            var sagaReloaded = _repo.GetById<DeliverPizzaSaga2>(sagaId);
            Assert.That(sagaReloaded.PizzaActualStatus, Is.EqualTo("Order Placed"));
            Assert.That(sagaReloaded.Id, Is.EqualTo("DeliverPizzaSaga2_Order_42"));
        }

        [Test]
        public void Verify_saga_reloaded_status_after_two_events()
        {
            var orderId = new OrderId(42);
            var evt = new OrderPlaced(orderId);

            ApplyEventOnSaga(evt);

            var evt2 = new BillPrinted(orderId);
            ApplyEventOnSaga(evt2);
            
            var sagaId = _listener.GetCorrelationId(evt);
            var sagaReloaded = _repo.GetById<DeliverPizzaSaga2>(sagaId);
            Assert.That(sagaReloaded.PizzaActualStatus, Is.EqualTo("Bill printed"));
        }

        [Test]
        public void Verify_saga_does_not_mix_events()
        {
            var orderId = new OrderId(42);
            var evt = new OrderPlaced(orderId);

            ApplyEventOnSaga(evt);

            var orderId2 = new OrderId(41);
            var evt2 = new BillPrinted(orderId2);
            ApplyEventOnSaga(evt2);

            var sagaId = _listener.GetCorrelationId(evt);
            var sagaReloaded = _repo.GetById<DeliverPizzaSaga2>(sagaId);
            Assert.That(sagaReloaded.PizzaActualStatus, Is.EqualTo("Order Placed"));
        }
    }

    
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using CQRS.Kernel.Events;
using CQRS.Shared.Events;
using MongoDB.Driver.Linq;
using NUnit.Framework;

namespace CQRS.Tests.ProjectionsTests
{
    [TestFixture]
    public class InProcessEventBusTests
    {
        private ConcurrentEventBus _eventBus;
        private WindsorContainer _container;
        private ConcurrentProjection _projection1;
        private ConcurrentProjection _projection2;
        private ConcurrentProjection _projection3;
        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _projection1 = new ConcurrentProjection("slot1");
            _projection2 = new ConcurrentProjection("slot2");
            _projection3 = new ConcurrentProjection("slot3");

            _container = new WindsorContainer();
            _container.Register(
                Component.For<ConcurrentEventBus>(),
                Component.For<IProjection, IEventHandler<InsertEvent>>().Instance(_projection1).Named("p1"),
                Component.For<IProjection, IEventHandler<InsertEvent>>().Instance(_projection2).Named("p2"),
                Component.For<IProjection, IEventHandler<InsertEvent>>().Instance(_projection3).Named("p3")
            );
            _eventBus = _container.Resolve<ConcurrentEventBus>();

        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _container.Dispose();
        }

        [Test]
        public void run()
        {
            foreach (var e in GetEvents())
            {
                _eventBus.Publish(e);
            }

            Assert.AreEqual(1, _projection1.InsertCounter);
            Assert.AreEqual(1, _projection2.InsertCounter);
            Assert.AreEqual(1, _projection3.InsertCounter);
        }

        private IEnumerable<DomainEvent> GetEvents()
        {
            yield return new InsertEvent();
        }
    }

    public class ConcurrentProjection : AbstractProjection, IEventHandler<InsertEvent>
    {
        public int InsertCounter = 0;
        private readonly string _slotName;

        public ConcurrentProjection(string slotName)
        {
            _slotName = slotName;
        }

        public override void Drop()
        {
            
        }

        public override void SetUp()
        {
        }

        public void On(InsertEvent e)
        {
            InsertCounter++;
        }

        public override string GetSlotName()
        {
            return _slotName;
        }
    }
}

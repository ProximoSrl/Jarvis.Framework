using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Bus.Rebus.Integration.Adapters;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Tests.BusTests.Handlers;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using NSubstitute;
using NUnit.Framework;
using Rebus;
using Rebus.Bus;
using Rebus.Handlers;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class ListenerSubscriptionsTests
    {
        ListenerRegistration _registration;
        IBus _fakeBus;
        IKernel _kernel;

        private void CreateSut(IKernel kernel = null, IProcessManagerListener[] listener = null)
        {
            _kernel = kernel ?? Substitute.For<IKernel>();
            _fakeBus = Substitute.For<IBus>();
            _registration = new ListenerRegistration(
                listener ?? new IProcessManagerListener[] { new SampleListener() },
                _fakeBus,
                _kernel
            );
        }

        [Test]
        public void Messages_should_be_subscripted()
        {
            CreateSut();
            _registration.Subscribe();
            _fakeBus.Received().Subscribe(typeof(SampleMessage));
            _kernel.Received().Register(Arg.Is<IRegistration[]>(x =>
                x.Length == 1 && 
                x[0] is ComponentRegistration
            ));
        }

        [Test]
        public void IMessageHandler_should_subscribe()
        {
            WindsorContainer container = new WindsorContainer();
            container.Register(Component
                .For<IHandleMessages<SampleMessage>>()
                .ImplementedBy<SampleMessageHandler>());
            CreateSut(container.Kernel, new IProcessManagerListener[0]);
            _registration.Subscribe();

            _fakeBus.Received().Subscribe(typeof(SampleMessage));
        }
    }
}

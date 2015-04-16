using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Jarvis.Framework.Bus.Rebus.Integration.Adapters;
using Jarvis.Framework.Kernel.Engine;
using NSubstitute;
using NUnit.Framework;
using Rebus;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class ListenerSubscriptionsTests
    {
        ListenerRegistration _registration;
        IBus _fakeBus;
        IKernel _fakeKernel;

        [SetUp]
        public void SetUp()
        {
            _fakeKernel = Substitute.For<IKernel>();

            _fakeBus = Substitute.For<IBus>();
            _registration = new ListenerRegistration(
                new IProcessManagerListener[]{new SampleListener()}, 
                _fakeBus, 
                _fakeKernel
            );
        }

        [Test]
        public void messages_should_be_subscripted()
        {
            _registration.Subscribe();
            _fakeBus.Received().Subscribe<SampleMessage>();
            _fakeKernel.Received().Register(Arg.Is<IRegistration[]>(x =>
                x.Length == 1 && 
                x[0] is ComponentRegistration
            ));
        }
    }
}

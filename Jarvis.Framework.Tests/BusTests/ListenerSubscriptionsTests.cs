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
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class ListenerSubscriptionsTests
    {
        private ListenerRegistration _registration;
        private IBus _fakeBus;
        private IKernel _kernel;

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
        public async Task Messages_should_be_subscripted()
        {
            CreateSut();
            await _registration.SubscribeAsync().ConfigureAwait(false);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			_fakeBus.Received().Subscribe(typeof(SampleMessage));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			_kernel.Received().Register(Arg.Is<IRegistration[]>(x =>
                x.Length == 1 && 
                x[0] is ComponentRegistration
            ));
        }

        [Test]
        public async Task IMessageHandler_should_subscribe()
        {
            WindsorContainer container = new WindsorContainer();
            container.Register(Component
                .For<IHandleMessages<SampleMessage>>()
                .ImplementedBy<SampleMessageHandler>());
            CreateSut(container.Kernel, new IProcessManagerListener[0]);
            await _registration.SubscribeAsync().ConfigureAwait(false);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			_fakeBus.Received().Subscribe(typeof(SampleMessage));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
		}
	}
}

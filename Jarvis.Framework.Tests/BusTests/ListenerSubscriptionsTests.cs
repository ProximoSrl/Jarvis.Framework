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
        private ProcessManagerSubscriptionManager _processManagerSubscriptionManager;
		private ProcessManagerCastleListenerRegistration _processManagerCastleListenerRegistration;

		private IBus _fakeBus;
        private IKernel _kernel;

        private void CreateSut(IKernel kernel = null, IProcessManagerListener[] listener = null)
        {
            _kernel = kernel ?? Substitute.For<IKernel>();
            _fakeBus = Substitute.For<IBus>();
            _processManagerSubscriptionManager = new ProcessManagerSubscriptionManager(
                listener ?? new IProcessManagerListener[] { new SampleListener() },
                _fakeBus,
                _kernel
            );

			_processManagerCastleListenerRegistration = new ProcessManagerCastleListenerRegistration(
				listener ?? new IProcessManagerListener[] { new SampleListener() },
				_kernel
			);
		}

        [Test]
        public async Task Messages_should_be_subscripted_in_rebus()
        {
            CreateSut();
            await _processManagerSubscriptionManager.SubscribeAsync().ConfigureAwait(false);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			_fakeBus.Received().Subscribe(typeof(SampleMessage));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

		[Test]
		public void Messages_should_be_registered_in_windsor_castle()
		{
			CreateSut();
			_processManagerCastleListenerRegistration.Subscribe();
			_kernel.Received().Register(Arg.Is<IRegistration[]>(x =>
				x.Length == 1
				&& x[0] is ComponentRegistration
			));
		}

		[Test]
        public async Task IMessageHandler_should_subscribe_in_rebus()
        {
            WindsorContainer container = new WindsorContainer();
            container.Register(Component
                .For<IHandleMessages<SampleMessage>>()
                .ImplementedBy<SampleMessageHandler>());
            CreateSut(container.Kernel, new IProcessManagerListener[0]);
            await _processManagerSubscriptionManager.SubscribeAsync().ConfigureAwait(false);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			_fakeBus.Received().Subscribe(typeof(SampleMessage));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
		}
	}
}

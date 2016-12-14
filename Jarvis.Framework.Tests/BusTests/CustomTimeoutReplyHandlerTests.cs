using System;
using System.Threading;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Bus.Rebus.Integration.Support;
using NUnit.Framework;
using Rebus;
using Rebus.Castle.Windsor;
using Rebus.Configuration;
using Rebus.Messages;
using Rebus.Transports.Msmq;
using Jarvis.Framework.Tests.Support;

namespace Jarvis.Framework.Tests.BusTests
{
    public class SampleMessageHandler : IHandleMessages<SampleMessage>
    {
        public readonly ManualResetEvent Reset = new ManualResetEvent(false);
        public void Handle(SampleMessage message)
        {
            this.ReceivedMessage = message;
            Reset.Set();
        }

        public SampleMessage ReceivedMessage { get; private set; }
    }

    [TestFixture, Category("exclude_on_linux")]
    public class CustomTimeoutReplyHandlerTests
    {
        IBus _bus;
        WindsorContainer _container;
        SampleMessageHandler _handler;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            TestHelper.ClearAllQueue("cqrs.rebus.test", "cqrs.rebus.errors");
            _container = new WindsorContainer();
            var startableBus = Configure.With(new WindsorContainerAdapter(_container))
                .Transport(t => t.UseMsmqAndGetInputQueueNameFromAppConfig())
                .Timeouts(t => t.StoreInMemory())
                .MessageOwnership(d => d.FromRebusConfigurationSection())
                .SpecifyOrderOfHandlers(pipeline => pipeline.Use(new RemoveDefaultTimeoutReplyHandlerFilter()))
                .CreateBus();

            _handler = new SampleMessageHandler();
            _container.Register(
                Component
                    .For<IHandleMessages<TimeoutReply>>()
                    .ImplementedBy<CustomTimeoutReplyHandler>(),
                Component
                    .For<IHandleMessages<SampleMessage>>()
                    .Instance(_handler)
            );

            _bus = startableBus.Start();
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _bus.Dispose();
            _container.Dispose();
        }

        [Test]
        public void check_timeout()
        {
            _bus.Defer(TimeSpan.FromMilliseconds(300), new SampleMessage(10));
            var handled = _handler.Reset.WaitOne(10000);
            Assert.IsTrue(handled);
            Assert.AreEqual(10, _handler.ReceivedMessage.Id);
        }
    }
}

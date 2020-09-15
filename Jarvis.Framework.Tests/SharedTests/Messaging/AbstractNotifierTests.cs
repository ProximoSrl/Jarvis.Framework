using Jarvis.Framework.Tests.Support;

namespace Jarvis.Framework.Tests.SharedTests.Messaging
{
    [TestFixture("abstract")]
    [TestFixture("MongoDb")]
    public class AbstractNotifierTests
    {
        private WindsorContainer _container;
        private IMongoDatabase _db;
        private AbstractNotifierManager _sut;
        private readonly string _notifierToTest;

        public AbstractNotifierTests(String notifierToTest)
        {
            _notifierToTest = notifierToTest;
        }

        [SetUp]
        public Task SetUp()
        {
            _container = new WindsorContainer();
            _db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            if (_notifierToTest == "abstract")
            {
                _sut = new TestNotifierManager(_container.Kernel);
            }
            else
            {
                _db.Drop();
                _sut = new MongoDbNotifierManager(_container.Kernel, _db, "pollerTest", "id1");
            }
            return _sut.StartPollingAsync();
        }

        [Test]
        public async Task Test_no_one_is_registered_not_throw()
        {
            Assert.DoesNotThrowAsync(async () => await Send(new MessageA()));
        }

        [Test]
        public async Task Test_basic_registration()
        {
            Int32 callCount = 0;
            _sut.Subscribe<MessageA>(_ => callCount++);

            await Send(new MessageA());
            Assert.That(callCount, Is.EqualTo(1), "MEssage not dispatched");

            await Send(new MessageB());
            Assert.That(callCount, Is.EqualTo(1), "Wrong MEssage dispatched");
        }

        [Test]
        public async Task Verify_basic_castle_Registration()
        {
            _container.Register(Component.For<INotificationHandler<MessageA>>().ImplementedBy<MessageAHandler>());

            await Send(new MessageA());
            var handler = _container.Resolve<INotificationHandler<MessageA>>() as MessageAHandler;
            Assert.That(handler.Received.Count, Is.EqualTo(1), "MEssage not dispatched");
        }

        [Test]
        public async Task Verify_basic_castle_Registration_not_update_after_first_call()
        {
            var instance1 = new MessageAHandler();
            var instance2 = new MessageAHandler();

            _container.Register(Component.For<INotificationHandler<MessageA>>().Instance(instance1).Named("1"));
            await Send(new MessageA());
            _container.Register(Component.For<INotificationHandler<MessageA>>().Instance(instance2).Named("2"));

            Assert.That(instance1.Received.Count, Is.EqualTo(1));
            Assert.That(instance2.Received.Count, Is.EqualTo(0), "Cache should be active");

            _sut.ClearCache();
            await Send(new MessageA());
            Assert.That(instance1.Received.Count, Is.EqualTo(2));
            Assert.That(instance2.Received.Count, Is.EqualTo(1), "Cache should be active");
        }

        [Test]
        public async Task Test_multiple_registration()
        {
            Int32 callCount1 = 0;
            Int32 callCount2 = 0;
            _sut.Subscribe<MessageA>(_ => callCount1++);

            await Send(new MessageA());
            Assert.That(callCount1, Is.EqualTo(1), "MEssage not dispatched");
            Assert.That(callCount2, Is.EqualTo(0), "MEssage not dispatched");

            _sut.Subscribe<MessageA>(_ => callCount2++);

            await Send(new MessageA());
            Assert.That(callCount1, Is.EqualTo(2), "MEssage not dispatched");
            Assert.That(callCount2, Is.EqualTo(1), "MEssage not dispatched");
        }

        [Test]
        public async Task Test_multiple_registration_dispatched_when_one_throws()
        {
            Int32 callCount = 0;
            _sut.Subscribe<MessageA>(_ => callCount++);
            _sut.Subscribe<MessageA>(_ =>
            {
                callCount++;
                throw new NotImplementedException();
            });
            _sut.Subscribe<MessageA>(_ => callCount++);

            await Send(new MessageA());
            Assert.That(callCount, Is.EqualTo(3), "MEssage not dispatched");
        }

        private async Task Send(object obj)
        {
            if (_notifierToTest == "abstract")
            {
                ((TestNotifierManager)_sut).Send(obj);
            }
            else
            {
                var notifier = ((MongoDbNotifierManager)_sut);
                await notifier.Publish(obj);
                while (!notifier.ForcePoll())
                {
                    Console.Write("Poll skipped");
                }
            }
        }

        private class TestNotifierManager : AbstractNotifierManager
        {
            public TestNotifierManager(IKernel kernel) : base(kernel)
            {
            }

            public Task Send(object obj)
            {
                return base.Consume(obj);
            }

            public override Task StartPollingAsync()
            {
                return Task.CompletedTask;
            }

            public override Task StopPollingAsync()
            {
                return Task.CompletedTask;
            }
        }

        private class MessageAHandler : INotificationHandler<MessageA>
        {
            public List<MessageA> Received { get; set; } = new List<MessageA>();

            public Task Handle(MessageA message)
            {
                Received.Add(message);
                return Task.CompletedTask;
            }
        }

        private class MessageA
        {
        }

        private class MessageB
        {
        }
    }
}

#if NETFULL
using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Rebus.Adapters;
using Jarvis.Framework.Rebus.Support;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Commands.Tracking;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Support;
using Jarvis.Framework.Tests.BusTests.Handlers;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NUnit.Framework;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.BusTests
{
    [NonParallelizable]
    [TestFixture("msmq")]
    [TestFixture("mongodb")]
    public class MessageTrackerTests
    {
        private IBus _bus;
        private WindsorContainer _container;
        private SampleCommandHandler _handler;
        private IMongoCollection<TrackedMessageModel> _messages;
        private ICommandExecutionExceptionHelper _commandExecutionExceptionHelper;
        private readonly string _transportType;

        public MessageTrackerTests(string transportType)
        {
            _transportType = transportType;
        }

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            _container = new WindsorContainer();
            _container.Register(Component.For<Castle.Core.Logging.ILoggerFactory>().Instance(new Castle.Core.Logging.NullLogFactory()));
            _container.Kernel.ComponentRegistered += Kernel_ComponentRegistered;
            String connectionString = ConfigurationManager.ConnectionStrings["log"].ConnectionString;
            var rebusUrl = new MongoUrl(connectionString);
            var rebusClient = rebusUrl.CreateClient(false);
            var rebusDb = rebusClient.GetDatabase(rebusUrl.DatabaseName);
            _messages = rebusDb.GetCollection<TrackedMessageModel>("messages");
            MongoDbMessagesTracker tracker = new MongoDbMessagesTracker(rebusDb);

            JarvisRebusConfiguration configuration = new JarvisRebusConfiguration(connectionString, "test")
            {
                ErrorQueue = "jarvistest-errors",
                InputQueue = "jarvistest-input",
                MaxRetry = 3,
                NumOfWorkers = 1,
                EndpointsMap = new System.Collections.Generic.Dictionary<string, string>()
                {
                    { "Jarvis.Framework.Tests" , "jarvistest-input"}
                }
            };

            configuration.AssembliesWithMessages = new List<System.Reflection.Assembly>()
            {
                typeof(SampleMessage).Assembly,
            };
            BusBootstrapper bb = CreateBusBootstrapper(tracker, configuration);

            TestHelper.RegisterSerializerForFlatId<SampleAggregateId>();

            bb.Start();
            var rebusConfigurer = _container.Resolve<RebusConfigurer>();
            rebusConfigurer.Start();
            _bus = _container.Resolve<IBus>();

            _handler = new SampleCommandHandler();
            _commandExecutionExceptionHelper = new JarvisDefaultCommandExecutionExceptionHelper(NullLogger.Instance, 20, 10);
            var handlerAdapter = new MessageHandlerToCommandHandlerAdapter<SampleTestCommand>(_handler, _commandExecutionExceptionHelper, tracker, _bus, null);

            _container.Register(
                Component
                    .For<IHandleMessages<SampleTestCommand>>()
                    .Instance(handlerAdapter)
            );

            var handlerAggregateAdapter = new MessageHandlerToCommandHandlerAdapter<SampleAggregateTestCommand>(_handler, _commandExecutionExceptionHelper, tracker, _bus, null);

            _container.Register(
                Component
                    .For<IHandleMessages<SampleAggregateTestCommand>>()
                    .Instance(handlerAggregateAdapter)
            );
        }

        private BusBootstrapper CreateBusBootstrapper(MongoDbMessagesTracker tracker, JarvisRebusConfiguration configuration)
        {
            if (_transportType == "msmq")
            {
                return new MsmqTransportJarvisTestBusBootstrapper(
                    _container,
                    configuration,
                    tracker);
            }
            else
            {
                return new MongoDbTransportJarvisTestBusBootstrapper(
                    _container,
                    configuration,
                    tracker);
            }
        }

        private void Kernel_ComponentRegistered(string key, Castle.MicroKernel.IHandler handler)
        {
            if (handler.ComponentModel.Services.Contains(typeof(IBus)))
            {
                Console.WriteLine("Bus registered");
            }
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            _bus.Dispose();
            _container.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            _messages.Drop();
        }

        [Test]
        public async Task Check_basic_tracking()
        {
            var sampleMessage = new SampleTestCommand(10);
            await _bus.Send(sampleMessage).ConfigureAwait(false);
            _handler.Reset.WaitOne(10000);

            //cycle until we found handled message on tracking
            DateTime startTime = DateTime.Now;
            TrackedMessageModel track;
            do
            {
                Thread.Sleep(50);
                var tracks = _messages.FindAll().ToList();
                Assert.That(tracks, Has.Count.EqualTo(1));
                track = tracks.Single();
            }
            while (track.CompletedAt == null
                  && DateTime.Now.Subtract(startTime).TotalSeconds < 4
            );

            Assert.That(track.MessageId, Is.EqualTo(sampleMessage.MessageId.ToString()));
            Assert.That(track.Description, Is.EqualTo(sampleMessage.Describe()));
            Assert.That(track.StartedAt, Is.Not.Null);
            Assert.That(track.CompletedAt, Is.Not.Null);
            Assert.That(track.Completed, Is.True);
            Assert.That(track.Success, Is.True);
        }

        [Test]
        public async Task Check_tracking_of_aggregate_id()
        {
            var id = new SampleAggregateId(1);
            var sampleMessage = new SampleAggregateTestCommand(id);
            await _bus.Send(sampleMessage).ConfigureAwait(false);

            _handler.Reset.WaitOne(10000);

            Thread.Sleep(50);
            var track = _messages.AsQueryable().Single();

            Assert.That(track.AggregateId, Is.EqualTo(id.AsString()));
        }

        /// <summary>
        /// verify the capability to re-send on bus with reply to header
        /// and that the CommandHandled is a real command that was also tracked.
        /// </summary>
        [Test]
        public async Task Check_response_handled()
        {
            var sampleMessage = new SampleTestCommand(10);
            sampleMessage.SetContextData(MessagesConstants.ReplyToHeader, "test");
            await _bus.Send(sampleMessage).ConfigureAwait(false);
            _handler.Reset.WaitOne(10000);

            //cycle until we found handled message on tracking
            List<TrackedMessageModel> tracks = null;
            DateTime startTime = DateTime.Now;
            do
            {
                Thread.Sleep(50);
                tracks = _messages.FindAll().ToList();
            }
            while (
                    tracks.Count < 2 && //command tracking and commandhandled tracking
                    DateTime.Now.Subtract(startTime).TotalSeconds < 4
            );

            var track = tracks.Single(t => t.MessageId == sampleMessage.MessageId.ToString());
            Assert.That(track.Description, Is.EqualTo(sampleMessage.Describe()));
            Assert.That(track.StartedAt, Is.Not.Null);
            Assert.That(track.CompletedAt, Is.Not.Null);
            Assert.That(track.Completed, Is.True);
            Assert.That(track.Success, Is.True);

            var handledTrack = tracks.Single(t => t.MessageId != sampleMessage.MessageId.ToString());
            CommandHandled commandHandled = (CommandHandled)handledTrack.Message;
            Assert.That(commandHandled.CommandId, Is.EqualTo(sampleMessage.MessageId));
            Assert.That(handledTrack.Description, Does.Contain(sampleMessage.MessageId.ToString()));
            Assert.That(handledTrack.Description, Does.Contain("Handled!"));
        }

        /// <summary>
        /// verify the capability to re-send on bus with reply to header
        /// and that the CommandHandled is a real command that was also tracked.
        /// </summary>
        [Test]
        public async Task Verify_elaboration_tracking()
        {
            var sampleMessage = new SampleTestCommand(10);
            await _bus.Send(sampleMessage).ConfigureAwait(false);
            _handler.Reset.WaitOne(10000);

            //cycle until we found handled message on tracking
            TrackedMessageModel track;
            do
            {
                Thread.Sleep(50);
                var tracks = _messages.FindAll().ToList();
                track = tracks.Single();
            }
            while (
                    track.CompletedAt == null
                    && DateTime.Now.Subtract(DateTime.Now).TotalSeconds < 4
            );

            Assert.That(track.MessageId, Is.EqualTo(sampleMessage.MessageId.ToString()));
            Assert.That(track.LastExecutionStartTime, Is.Not.Null);
            Assert.That(track.ExecutionCount, Is.EqualTo(1));
            Assert.That(track.ExecutionStartTimeList.Length, Is.EqualTo(1));
        }

        /// <summary>
        /// Deferred messages should not be logged in messages collection ,that message
        /// collection is meant to contains only command that are waiting in the queue.
        /// </summary>
        [Test]
        public async Task Verify_deferred_message_are_not_tracked()
        {
            var sampleMessage = new SampleTestCommand(10);
            await _bus.Defer(TimeSpan.FromSeconds(2), sampleMessage).ConfigureAwait(false);

            var mid = sampleMessage.MessageId.ToString();
            var trackedMessage = _messages.AsQueryable().SingleOrDefault(m => m.MessageId == mid);
            Assert.That(trackedMessage, Is.Null);

            DateTime waitStart = DateTime.UtcNow;
            //now wait some seconds
            do
            {
                Thread.Sleep(50);
                trackedMessage = _messages.AsQueryable().SingleOrDefault(m => m.MessageId == mid);
                if (trackedMessage != null)
                {
                    //test is ok
                    return;
                }
            }
            while (DateTime.UtcNow.Subtract(waitStart).TotalSeconds < 5);

            Assert.Fail("MEssage is not tracked after defer time passed");
        }
    }
}
#endif

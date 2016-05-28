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
using System.Configuration;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;
using System.Linq;
using Jarvis.Framework.Bus.Rebus.Integration.Adapters;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using System.Collections.Generic;

namespace Jarvis.Framework.Tests.BusTests
{
    public class SampleCommandHandler : ICommandHandler<SampleTestCommand>
    {
        public readonly ManualResetEvent Reset = new ManualResetEvent(false);

        public void Handle(SampleTestCommand command)
        {
            this.ReceivedCommand = command;
            Reset.Set();
        }

        public void Handle(ICommand command)
        {
            Handle(command as SampleTestCommand);
        }

        public SampleTestCommand ReceivedCommand { get; private set; }
    }

    [TestFixture]
    public class MessageTrackerTests
    {
        IBus _bus;
        WindsorContainer _container;
        SampleCommandHandler _handler;
        MongoCollection<TrackedMessageModel> _messages;
        MessageHandlerToCommandHandlerAdapter<SampleTestCommand> _handlerAdapter;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _container = new WindsorContainer();
            String connectionString = ConfigurationManager.ConnectionStrings["rebus"].ConnectionString;
            var rebusUrl = new MongoUrl(connectionString);
            var rebusClient = new MongoClient(rebusUrl);
            var rebusDb = rebusClient.GetServer().GetDatabase(rebusUrl.DatabaseName);
            _messages = rebusDb.GetCollection<TrackedMessageModel>("messages");
            MongoDbMessagesTracker tracker = new MongoDbMessagesTracker(rebusDb);
            BusBootstrapper bb = new BusBootstrapper(
                _container,
                connectionString,
                "test",
                tracker);
            JarvisRebusConfiguration configuration = new JarvisRebusConfiguration()
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
            bb.Configuration = configuration;
            bb.StartWithConfigurationProperty(true);
            _bus = _container.Resolve<IBus>();
            _handler = new SampleCommandHandler();
            _handlerAdapter = new MessageHandlerToCommandHandlerAdapter<SampleTestCommand>(
                _handler, tracker, _bus);

            _container.Register(
                Component
                    .For<IHandleMessages<SampleTestCommand>>()
                    .Instance(_handlerAdapter)
            );
        }

        [TestFixtureTearDown]
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
        public void check_basic_tracking()
        {
            var sampleMessage = new SampleTestCommand(10);
            _bus.Send(sampleMessage);
            var handled = _handler.Reset.WaitOne(10000);
            //cycle until we found handled message on tracking
            TrackedMessageModel track = null;
            DateTime startTime = DateTime.Now;
            do
            {
                Thread.Sleep(50);
                var tracks = _messages.FindAll().ToList();
                Assert.That(tracks, Has.Count.EqualTo(1));
                track = tracks.Single();
            }
            while (
                    track.CompletedAt == null &&
                    DateTime.Now.Subtract(startTime).TotalSeconds < 4
            );

            Assert.That(track.MessageId, Is.EqualTo(sampleMessage.MessageId.ToString()));
            Assert.That(track.Description, Is.EqualTo(sampleMessage.Describe()));
            Assert.That(track.StartedAt, Is.Not.Null);
            Assert.That(track.CompletedAt, Is.Not.Null);
        }

        /// <summary>
        /// verify the capability to re-send on bus with reply to header
        /// and that the CommandHandled is a real command that was also tracked.
        /// 
        /// </summary>
        [Test]
        public void check_response_handled()
        {
            var sampleMessage = new SampleTestCommand(10);
            sampleMessage.SetContextData(MessagesConstants.ReplyToHeader, "test");
            _bus.Send(sampleMessage);
            var handled = _handler.Reset.WaitOne(10000);
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

            var handledTrack = tracks.Single(t => t.MessageId != sampleMessage.MessageId.ToString());
            CommandHandled commandHandled = (CommandHandled)handledTrack.Message;
            Assert.That(commandHandled.CommandId, Is.EqualTo(sampleMessage.MessageId));
            Assert.That(handledTrack.Description, Is.StringContaining(sampleMessage.MessageId.ToString()));
            Assert.That(handledTrack.Description, Is.StringContaining("Handled!"));
        }
    }
}

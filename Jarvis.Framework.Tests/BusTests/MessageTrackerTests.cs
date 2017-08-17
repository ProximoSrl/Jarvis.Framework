using System;
using System.Threading;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Bus.Rebus.Integration.Support;
using NUnit.Framework;
using System.Configuration;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;
using System.Linq;
using Jarvis.Framework.Bus.Rebus.Integration.Adapters;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using Jarvis.Framework.Shared.Messages;
using System.Collections.Generic;
using Jarvis.Framework.Shared.Helpers;
using Rebus.Bus;
using Rebus.Handlers;
using Jarvis.Framework.Tests.BusTests.Handlers;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class MessageTrackerTests
    {
        private IBus _bus;
        private WindsorContainer _container;
        private SampleCommandHandler _handler;
        private IMongoCollection<TrackedMessageModel> _messages;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _container = new WindsorContainer();
            _container.Kernel.ComponentRegistered += Kernel_ComponentRegistered;
            String connectionString = ConfigurationManager.ConnectionStrings["log"].ConnectionString;
            var rebusUrl = new MongoUrl(connectionString);
            var rebusClient = new MongoClient(rebusUrl);
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

            BusBootstrapper bb = new BusBootstrapper(
                _container,
                configuration,
                tracker);

            bb.Start();
            var startableBus = _container.Resolve<IStartableBus>();
            startableBus.Start();
            _bus = _container.Resolve<IBus>();

            _handler = new SampleCommandHandler();
            var handlerAdapter = new MessageHandlerToCommandHandlerAdapter<SampleTestCommand>(_handler, tracker, _bus);

            _container.Register(
                Component
                    .For<IHandleMessages<SampleTestCommand>>()
                    .Instance(handlerAdapter)
            );
        }

        private void Kernel_ComponentRegistered(string key, Castle.MicroKernel.IHandler handler)
        {
            if (handler.ComponentModel.Services.Contains(typeof(IBus)))
            {
                Console.WriteLine("Bus registered");
            }
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
        public void Check_basic_tracking()
        {
            var sampleMessage = new SampleTestCommand(10);
            _bus.Send(sampleMessage);
            _handler.Reset.WaitOne(10000);

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
            Assert.That(track.Completed, Is.True);
            Assert.That(track.Success, Is.True);
        }

        /// <summary>
        /// verify the capability to re-send on bus with reply to header
        /// and that the CommandHandled is a real command that was also tracked.
        /// 
        /// </summary>
        [Test]
        public void Check_response_handled()
        {
            var sampleMessage = new SampleTestCommand(10);
            sampleMessage.SetContextData(MessagesConstants.ReplyToHeader, "test");
            _bus.Send(sampleMessage);
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
            Assert.That(handledTrack.Description, Is.StringContaining(sampleMessage.MessageId.ToString()));
            Assert.That(handledTrack.Description, Is.StringContaining("Handled!"));
        }

        /// <summary>
        /// verify the capability to re-send on bus with reply to header
        /// and that the CommandHandled is a real command that was also tracked.
        /// 
        /// </summary>
        [Test]
        public void Verify_elaboration_tracking()
        {
            var sampleMessage = new SampleTestCommand(10);
            _bus.Send(sampleMessage);
            _handler.Reset.WaitOne(10000);

            //cycle until we found handled message on tracking
            TrackedMessageModel track = null;
            DateTime startTime = DateTime.Now;
            do
            {
                Thread.Sleep(50);
                var tracks = _messages.FindAll().ToList();
                track = tracks.Single();
            }
            while (
                    track.CompletedAt == null &&
                    DateTime.Now.Subtract(startTime).TotalSeconds < 4
            );

            Assert.That(track.MessageId, Is.EqualTo(sampleMessage.MessageId.ToString()));
            Assert.That(track.LastExecutionStartTime, Is.Not.Null);
            Assert.That(track.ExecutionCount, Is.EqualTo(1));
            Assert.That(track.ExecutionStartTimeList.Length, Is.EqualTo(1));
        }
    }
}

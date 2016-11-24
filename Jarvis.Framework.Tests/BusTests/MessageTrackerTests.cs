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
using Jarvis.Framework.Shared.Helpers;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

using MongoDB.Driver.Linq;
using NEventStore.Domain.Persistence;

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

    public class AnotherSampleCommandHandler : ICommandHandler<AnotherSampleTestCommand>
    {
        public readonly ManualResetEvent Reset = new ManualResetEvent(false);

        public static Int32 failCount = 0;

        public static Int32 numOfFailure = 2; //Fail two times before success

        public static Exception exceptionToThrow;

        public static Boolean setEventOnThrow = false;

        public AnotherSampleCommandHandler()
        {

        }
        public void Handle(AnotherSampleTestCommand command)
        {
            if (numOfFailure > 0 && numOfFailure > failCount)
            {
                failCount++;
                if (setEventOnThrow) Reset.Set();
                throw exceptionToThrow;
            }
            this.ReceivedCommand = command;
            Reset.Set();
        }

        public void Handle(ICommand command)
        {
            Handle(command as AnotherSampleTestCommand);
        }

        public AnotherSampleTestCommand ReceivedCommand { get; private set; }
    }

    [TestFixture]
    public class MessageTrackerTests
    {
        IBus _bus;
        WindsorContainer _container;
        SampleCommandHandler _handler;
        IMongoCollection<TrackedMessageModel> _messages;
        MessageHandlerToCommandHandlerAdapter<SampleTestCommand> _handlerAdapter;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _container = new WindsorContainer();
            
            String connectionString = ConfigurationManager.ConnectionStrings["log"].ConnectionString;
            var rebusUrl = new MongoUrl(connectionString);
            var rebusClient = new MongoClient(rebusUrl);
            var rebusDb = rebusClient.GetDatabase(rebusUrl.DatabaseName);
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
            bb.Start();
            var startableBus = _container.Resolve<IStartableBus>();
            startableBus.Start();
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

        /// <summary>
        /// verify the capability to re-send on bus with reply to header
        /// and that the CommandHandled is a real command that was also tracked.
        /// 
        /// </summary>
        [Test]
        public void verify_elaboration_tracking()
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
            Assert.That(track.LastExecutionStartTime, Is.Not.Null);
            Assert.That(track.ExecutionCount, Is.EqualTo(1));
            Assert.That(track.ExecutionStartTimeList.Length, Is.EqualTo(1));
        }
    }

    [TestFixture]
    public class MessageTrackerFithFailureTests
    {
        IBus _bus;
        WindsorContainer _container;
        AnotherSampleCommandHandler _handler;
        IMongoCollection<TrackedMessageModel> _messages;
        MessageHandlerToCommandHandlerAdapter<AnotherSampleTestCommand> _handlerAdapter;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _container = new WindsorContainer();
            String connectionString = ConfigurationManager.ConnectionStrings["log"].ConnectionString;
            var logUrl = new MongoUrl(connectionString);
            var logClient = new MongoClient(logUrl);
            var logDb = logClient.GetDatabase(logUrl.DatabaseName);
            _messages = logDb.GetCollection<TrackedMessageModel>("messages");
            MongoDbMessagesTracker tracker = new MongoDbMessagesTracker(logDb);
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
            bb.Start();
            var startableBus = _container.Resolve<IStartableBus>();
            startableBus.Start();

            _bus = _container.Resolve<IBus>();
            _handler = new AnotherSampleCommandHandler();
            _handlerAdapter = new MessageHandlerToCommandHandlerAdapter<AnotherSampleTestCommand>(
                _handler, tracker, _bus);

            _container.Register(
                Component
                    .For<IHandleMessages<AnotherSampleTestCommand>>()
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
            AnotherSampleCommandHandler.failCount = 0;
            AnotherSampleCommandHandler.numOfFailure = 0;
            AnotherSampleCommandHandler.exceptionToThrow = null;
        }

        [Test]
        public void verify_failure_tracking()
        {
            AnotherSampleCommandHandler.numOfFailure = 1;
            AnotherSampleCommandHandler.exceptionToThrow = new DomainException("TEST_1", "Test Exception", null);
            AnotherSampleCommandHandler.setEventOnThrow = true;

            var sampleMessage = new AnotherSampleTestCommand(10);
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
                    track.FailedAt == null &&
                    DateTime.Now.Subtract(startTime).TotalSeconds < 4
            );

            Assert.That(track.MessageId, Is.EqualTo(sampleMessage.MessageId.ToString()));
            Assert.That(track.Description, Is.EqualTo(sampleMessage.Describe()));
            Assert.That(track.StartedAt, Is.Not.Null);
            Assert.That(track.CompletedAt, Is.Null);
            Assert.That(track.FailedAt, Is.Not.Null);
            Assert.That(track.DispatchedAt, Is.Null);
        }

        [Test]
        public void verify_retry_start_execution_tracking()
        {
            AnotherSampleCommandHandler.numOfFailure = 2;
            AnotherSampleCommandHandler.exceptionToThrow = new ConflictingCommandException("TEST_1", null);
            AnotherSampleCommandHandler.setEventOnThrow = false;

            var sampleMessage = new AnotherSampleTestCommand(10);
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
                    track.ExecutionCount < AnotherSampleCommandHandler.numOfFailure &&
                    DateTime.Now.Subtract(startTime).TotalSeconds < 4
            );

            Assert.That(track.MessageId, Is.EqualTo(sampleMessage.MessageId.ToString()));
            Assert.That(track.Description, Is.EqualTo(sampleMessage.Describe()));
            Assert.That(track.StartedAt, Is.Not.Null);
            Assert.That(track.CompletedAt, Is.Not.Null);
            Assert.That(track.FailedAt, Is.Null);
            Assert.That(track.DispatchedAt, Is.Null);

            Assert.That(track.LastExecutionStartTime, Is.Not.Null);
            Assert.That(track.ExecutionCount, Is.EqualTo(3));
            Assert.That(track.ExecutionStartTimeList.Length, Is.EqualTo(3));
        }
    }

    [TestFixture]
    public class MongoDbMessagesTrackerTests
    {
        MongoDbMessagesTracker sut;
        IMongoCollection<TrackedMessageModel> _messages;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            String connectionString = ConfigurationManager.ConnectionStrings["log"].ConnectionString;
            var logUrl = new MongoUrl(connectionString);
            var logClient = new MongoClient(logUrl);
            var logDb = logClient.GetDatabase(logUrl.DatabaseName);
            sut = new MongoDbMessagesTracker(logDb);
            _messages = logDb.GetCollection<TrackedMessageModel>("messages");
        }

        [SetUp]
        public void SetUp()
        {
            sut.Drop();
        }


        [Test]
        public void verify_multiple_start_execution_time()
        {
            SampleTestCommand cmd = new SampleTestCommand(1);
            sut.Started(cmd);
            DateTime startDate1 = new DateTime(2000, 01, 01, 1, 1, 42, DateTimeKind.Utc);
            DateTime startDate2 = startDate1.AddSeconds(1);
            sut.ElaborationStarted(cmd.MessageId, startDate1);
            sut.ElaborationStarted(cmd.MessageId, startDate2);

            var handledTrack = _messages.AsQueryable().Single(t => t.MessageId == cmd.MessageId.ToString());
            Assert.That(handledTrack.LastExecutionStartTime, Is.EqualTo(startDate2));
            Assert.That(handledTrack.ExecutionStartTimeList, Is.EquivalentTo(new[] { startDate1, startDate2 }));
        }
    }
}

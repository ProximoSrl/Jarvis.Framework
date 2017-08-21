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
using Jarvis.NEventStoreEx.CommonDomainEx.Core;
using NEventStore.Domain.Persistence;
using MongoDB.Bson;
using Rebus.Bus;
using Rebus.Handlers;
using Jarvis.Framework.Tests.BusTests.Handlers;
using Jarvis.Framework.Shared.Helpers;
using Rebus.Config;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class MessageTrackerFithFailureTests
    {
        private IBus _bus;
        private WindsorContainer _container;
        private AnotherSampleCommandHandler _handler;
        private IMongoCollection<TrackedMessageModel> _messages;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _container = new WindsorContainer();
            String connectionString = ConfigurationManager.ConnectionStrings["log"].ConnectionString;
            var logUrl = new MongoUrl(connectionString);
            var logClient = new MongoClient(logUrl);
            var logDb = logClient.GetDatabase(logUrl.DatabaseName);
            _messages = logDb.GetCollection<TrackedMessageModel>("messages");

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

            MongoDbMessagesTracker tracker = new MongoDbMessagesTracker(logDb);
            BusBootstrapper bb = new BusBootstrapper(
                _container,
                configuration,
                tracker);

            bb.Start();
            var rebusConfigurer = _container.Resolve<RebusConfigurer>();
            rebusConfigurer.Start();

            _bus = _container.Resolve<IBus>();
            _handler = new AnotherSampleCommandHandler();
            var handlerAdapter = new MessageHandlerToCommandHandlerAdapter<AnotherSampleTestCommand>(
                _handler, tracker, _bus, 200);

            _container.Register(
                Component
                    .For<IHandleMessages<AnotherSampleTestCommand>>()
                    .Instance(handlerAdapter)
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

        /// <summary>
        /// When a domain exception is raised, we expect only one failure
        /// </summary>
        [Test]
        public void Verify_failure_tracking_for_domain_exception()
        {
            //We decide to fail only one time
            var sampleMessage = new AnotherSampleTestCommand(1, "verify_failure_tracking_for_domain_exception");
            AnotherSampleCommandHandler.SetFixtureData(sampleMessage.MessageId, 1, new DomainException("TEST_1", "Test Exception for verify_failure_tracking_for_domain_exception test", null), true);
            _bus.Send(sampleMessage);
            _handler.Reset.WaitOne(10000);

            //cycle until we found handled message on tracking with specified condition
            TrackedMessageModel track;
            DateTime startTime = DateTime.Now;
            do
            {
                Thread.Sleep(200);
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString() &&
                    t.ExecutionCount == 1 &&
                    t.Completed == true &&
                    t.Success == false);
            }
            while (
                    track == null &&
                    DateTime.Now.Subtract(startTime).TotalSeconds < 4
            );

            if (track == null)
            {
                //failure, try to recover the message id to verify what was wrong
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString());
            }
            //donormal assertion
            if (track == null)
            {
                throw new AssertionException($"Message {sampleMessage.MessageId} did not generate tracking info");
            }

            var allTracking = _messages.FindAll().ToList();
            if (allTracking.Count != 1)
            {
                Console.WriteLine($"Too many tracking elements: {allTracking.Count}");
                foreach (var t in allTracking)
                {
                    Console.WriteLine(t.ToBsonDocument());
                }
            }

            Assert.That(track.MessageId, Is.EqualTo(sampleMessage.MessageId.ToString()));
            Assert.That(track.Description, Is.EqualTo(sampleMessage.Describe()));
            Assert.That(track.StartedAt, Is.Not.Null);
            Assert.That(track.CompletedAt, Is.Not.Null);
            Assert.That(track.DispatchedAt, Is.Null);
            Assert.That(track.ErrorMessage, Is.Not.Empty);
            Assert.That(track.Completed, Is.True);
            Assert.That(track.Success, Is.False);
            Assert.That(track.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public void Verify_retry_start_execution_tracking()
        {
            var tracks = _messages.FindAll().ToList();
            Assert.That(tracks, Has.Count.EqualTo(0), "messages collection is not empty");

            var sampleMessage = new AnotherSampleTestCommand(2, "verify_retry_start_execution_tracking");
            AnotherSampleCommandHandler.SetFixtureData(sampleMessage.MessageId, 2, new ConflictingCommandException("TEST_1", null), true);

            _bus.Send(sampleMessage);
            _handler.Reset.WaitOne(10000);
            //cycle until we found handled message on tracking
            TrackedMessageModel track;
            DateTime startTime = DateTime.Now;
            {
                Thread.Sleep(200);
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString() &&
                    t.Completed == true);
            }
            while (
                    track == null && DateTime.Now.Subtract(startTime).TotalSeconds < 5
            ) ;

            if (track == null)
            {
                //failure, try to recover the message id to verify what was wrong
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString());
            }

            Assert.That(track.MessageId, Is.EqualTo(sampleMessage.MessageId.ToString()));
            Assert.That(track.Description, Is.EqualTo(sampleMessage.Describe()));
            Assert.That(track.StartedAt, Is.Not.Null);
            Assert.That(track.CompletedAt, Is.Not.Null);
            Assert.That(track.DispatchedAt, Is.Null);
            Assert.That(track.Completed, Is.True);
            Assert.That(track.Success, Is.True);

            Assert.That(track.LastExecutionStartTime, Is.Not.Null);
            Assert.That(track.ExecutionCount, Is.EqualTo(3));
            Assert.That(track.ExecutionStartTimeList.Length, Is.EqualTo(3));
        }

        /// <summary>
        /// This should be refactored to be quicker, to wait for all execution we need to wait
        /// all the Thread random sleep.
        /// </summary>
        [Test]
        public void Verify_exceeding_retry()
        {
            var sampleMessage = new AnotherSampleTestCommand(3, "verify_exceeding_retry");
            AnotherSampleCommandHandler.SetFixtureData(sampleMessage.MessageId, Int32.MaxValue, new ConflictingCommandException("TEST_1", null), false);
            _bus.Send(sampleMessage);
            _handler.Reset.WaitOne(10000);
            //cycle until we found handled message on tracking
            TrackedMessageModel track;
            DateTime startTime = DateTime.Now;
            do
            {
                Thread.Sleep(200);
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString() &&
                    t.ExecutionCount == 100 &&
                    t.Completed == true);
            }
            while (
                    track == null && DateTime.Now.Subtract(startTime).TotalSeconds < 60 //TODO: find a better way to handle this test.
            );

            if (track == null)
            {
                //failure, try to recover the message id to verify what was wrong
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString());
            }

            if (track == null)
            {
                throw new AssertionException($"Message {sampleMessage.MessageId} did not generate tracking info");
            }

            Assert.That(track.MessageId, Is.EqualTo(sampleMessage.MessageId.ToString()));
            Assert.That(track.Description, Is.EqualTo(sampleMessage.Describe()));
            Assert.That(track.StartedAt, Is.Not.Null);
            Assert.That(track.CompletedAt, Is.Not.Null);
            Assert.That(track.Completed, Is.True);
            Assert.That(track.Success, Is.False);

            Assert.That(track.ExecutionCount, Is.EqualTo(100));
        }
    }
}

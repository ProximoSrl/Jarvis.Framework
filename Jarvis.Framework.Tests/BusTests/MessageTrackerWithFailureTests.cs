#if NETFULL
using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Bus.Rebus.Integration.Adapters;
using Jarvis.Framework.Bus.Rebus.Integration.Support;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Commands.Tracking;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Support;
using Jarvis.Framework.Tests.BusTests.Handlers;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using MongoDB.Bson;
using MongoDB.Driver;
using NStore.Core.Streams;
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
    /// <summary>
    /// This tests the handler for the bus.
    /// </summary>
    [NonParallelizable]
    [TestFixture("msmq")]
    [TestFixture("mongodb")]
    public class MessageTrackerWithFailureTests
    {
        private const int RebusMaxRetry = 5;
        private IBus _bus;
        private WindsorContainer _container;
        private IMongoCollection<TrackedMessageModel> _messages;

        private readonly string _transportType;

        public MessageTrackerWithFailureTests(string transportType)
        {
            _transportType = transportType;
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

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            _container = new WindsorContainer();
            String connectionString = ConfigurationManager.ConnectionStrings["log"].ConnectionString;
            var logUrl = new MongoUrl(connectionString);
            var logClient = logUrl.CreateClient(false);
            var logDb = logClient.GetDatabase(logUrl.DatabaseName);
            logDb.Drop();

            _messages = logDb.GetCollection<TrackedMessageModel>("messages");

            JarvisRebusConfiguration configuration = new JarvisRebusConfiguration(connectionString, "test")
            {
                ErrorQueue = "jarvistest-errors",
                InputQueue = "jarvistest-input",
                MaxRetry = RebusMaxRetry,
                NumOfWorkers = 1,
                EndpointsMap = new System.Collections.Generic.Dictionary<string, string>()
                {
                    ["Jarvis.Framework.Tests"] = "jarvistest-input"
                }
            };

            configuration.AssembliesWithMessages = new List<System.Reflection.Assembly>()
            {
                typeof(SampleMessage).Assembly,
            };

            MongoDbMessagesTracker tracker = new MongoDbMessagesTracker(logDb);
            BusBootstrapper bb = CreateBusBootstrapper(tracker, configuration);

            bb.Start();
            var rebusConfigurer = _container.Resolve<RebusConfigurer>();
            rebusConfigurer.Start();

            _bus = _container.Resolve<IBus>();
            var handler = new AnotherSampleCommandHandler();

            var commandExecutionExceptionHelper = new JarvisDefaultCommandExecutionExceptionHelper(NullLogger.Instance, 20, 10);
            var handlerAdapter = new MessageHandlerToCommandHandlerAdapter<AnotherSampleTestCommand>(
                handler, commandExecutionExceptionHelper, tracker, _bus);

            _container.Register(
                Component
                    .For<IHandleMessages<AnotherSampleTestCommand>>()
                    .Instance(handlerAdapter)
            );
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

        /// <summary>
        /// When a domain exception is raised, we expect only one failure
        /// </summary>
        /// <param name="wrapInAggregateException"></param>
        [TestCase(true)]
        [TestCase(false)]
        public async Task Verify_failure_tracking_for_domain_exception(Boolean wrapInAggregateException)
        {
            //We decide to fail only one time
            var sampleMessage = new AnotherSampleTestCommand(1, "verify_failure_tracking_for_domain_exception");
            AnotherSampleCommandHandler.SetFixtureData(
                sampleMessage.MessageId,
                10,
                new DomainException("TEST_1", "Test Exception for verify_failure_tracking_for_domain_exception test", null),
                true,
                wrapInAggregateException: wrapInAggregateException);
            await _bus.Send(sampleMessage).ConfigureAwait(false);

            //cycle until we found handled message on tracking with specified condition
            TrackedMessageModel track;
            DateTime startTime = DateTime.Now;
            do
            {
                Thread.Sleep(200);
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString() &&
                    t.ExecutionCount == 1
                    && t.Completed == true
                    && t.Success == false);
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

        /// <summary>
        /// When a domain exception is raised, we expect only one failure
        /// </summary>
        /// <param name="wrapInAggregateException"></param>
        [TestCase(true)]
        [TestCase(false)]
        public async Task Verify_failure_tracking_flatten_aggregate_exception(Boolean wrapInAggregateException)
        {
            var sampleMessage = new AnotherSampleTestCommand(1, "verify_failure_tracking_for_domain_exception");
            AnotherSampleCommandHandler.SetFixtureData(
                sampleMessage.MessageId,
                10,
                new DomainException("TEST_1", "Test Exception for verify_failure_tracking_for_domain_exception test", null),
                true,
                wrapInAggregateException: wrapInAggregateException);
            await _bus.Send(sampleMessage).ConfigureAwait(false);

            //cycle until we found handled message on tracking with specified condition
            TrackedMessageModel track;
            DateTime startTime = DateTime.Now;
            do
            {
                Thread.Sleep(200);
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString() &&
                    t.ExecutionCount == 1
                    && t.Completed == true
                    && t.Success == false);
            }
            while (
                    track == null && DateTime.Now.Subtract(startTime).TotalSeconds < 4
            );

            if (track == null)
            {
                //failure, try to recover the message id to verify what was wrong
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString());
            }

            Assert.That(track.ErrorMessage, Is.EqualTo("Test Exception for verify_failure_tracking_for_domain_exception test"));
            Assert.That(track.FullException, Is.Not.Null);
            Assert.That(track.FullException.IndexOf("aggregate", StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1));
        }

        /// <summary>
        /// Verify what happens when handle launch exception for two times, than execution suceeds.
        /// </summary>
        /// <param name="wrapInAggregateException"></param>
        /// <returns></returns>
        [TestCase(true)]
        [TestCase(false)]
        public async Task Verify_retry_start_execution_tracking(Boolean wrapInAggregateException)
        {
            var tracks = _messages.FindAll().ToList();
            Assert.That(tracks, Has.Count.EqualTo(0), "messages collection is not empty");

            var sampleMessage = new AnotherSampleTestCommand(2, "verify_retry_start_execution_tracking");
            AnotherSampleCommandHandler.SetFixtureData(
                sampleMessage.MessageId,
                2,
                new ConcurrencyException("TEST_1", null),
                true,
                wrapInAggregateException: wrapInAggregateException);

            await _bus.Send(sampleMessage).ConfigureAwait(false);

            //cycle until we found handled message on tracking
            TrackedMessageModel track;
            DateTime startTime = DateTime.Now;
            do {
                Thread.Sleep(200);
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString()
                    && t.Completed == true);
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
        /// Verify that after too many exception the execution will halt.
        /// </summary>
        /// <param name="wrapInAggregateException"></param>
        [TestCase(true)]
        [TestCase(false)]
        public async Task Verify_exceeding_retry(Boolean wrapInAggregateException)
        {
            var sampleMessage = new AnotherSampleTestCommand(3, "verify_exceeding_retry");
            AnotherSampleCommandHandler.SetFixtureData(
                sampleMessage.MessageId,
                Int32.MaxValue,
                new ConcurrencyException("TEST_1", null),
                false,
                wrapInAggregateException: wrapInAggregateException);

            await _bus.Send(sampleMessage).ConfigureAwait(false);

            //cycle until we found handled message on tracking
            TrackedMessageModel track;
            DateTime startTime = DateTime.Now;
            do
            {
                Thread.Sleep(200);
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString()
                    && t.ExecutionCount == 10
                    && t.Completed == true);
            }
            while (
                    track == null && DateTime.Now.Subtract(startTime).TotalSeconds < 4
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

            Assert.That(track.ExecutionCount, Is.EqualTo(10), "Execution retry for conflict should do for 10 times");
        }

        /// <summary>
        /// Verify that after too many exception the execution will halt.
        /// </summary>
        /// <param name="wrapInAggregateException"></param>
        [TestCase(true)]
        [TestCase(false)]
        public async Task Verify_generic_exception_does_not_force_retry(Boolean wrapInAggregateException)
        {
            var sampleMessage = new AnotherSampleTestCommand(3, "verify_exceeding_retry");
            AnotherSampleCommandHandler.SetFixtureData(
                sampleMessage.MessageId,
                Int32.MaxValue, new JarvisFrameworkEngineException("TEST_1", null),
                false,
                wrapInAggregateException: wrapInAggregateException);

            await _bus.Send(sampleMessage).ConfigureAwait(false);

            //cycle until we found handled message on tracking
            TrackedMessageModel track;
            DateTime startTime = DateTime.Now;
            do
            {
                Thread.Sleep(200);
                track = _messages.AsQueryable().SingleOrDefault(t => t.MessageId == sampleMessage.MessageId.ToString()
                    && t.ExecutionCount >= RebusMaxRetry);
            }
            while (
                    track == null && DateTime.Now.Subtract(startTime).TotalSeconds < 5
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

            Assert.That(track.ExecutionCount, Is.EqualTo(RebusMaxRetry), $"{_transportType} Execution Should retry RebusMaxRetry times");
        }
    }
}
#endif
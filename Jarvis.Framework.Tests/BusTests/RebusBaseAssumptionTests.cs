using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Bus.Rebus.Integration.Support;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Tests.BusTests.Handlers;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using MongoDB.Driver;
using NUnit.Framework;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class RebusBaseAssumptionTests
    {
        String _connectionString;
        IMongoDatabase _db;

        [SetUp]
        public void SetUp()
        {
            PurgeQueue("jarvistest-input1");
            PurgeQueue("jarvistest-input2");

            _connectionString = ConfigurationManager.ConnectionStrings["rebus"].ConnectionString;
            var url = new MongoUrl(_connectionString);
            _db = new MongoClient(url).GetDatabase(url.DatabaseName);
            _db.Drop();
        }

        [Test]
        public async Task Verify_simple_publish_subscribe()
        {
            //This is the process 1, it knows that every message should be directed to queue number 2 and he 
            //listen on queue number 1, he is capable to handle the generic AnotherSampleMessageHandler.
            var mapping1 = new System.Collections.Generic.Dictionary<string, string>()
            {
                { "Jarvis.Framework.Tests" , "jarvistest-input2"} //Command is sent to input2
            };
            TestProcess process1 = new TestProcess(_connectionString, "jarvistest-input1", mapping1);
            var _sampleMessageHandler1 = new AnotherSampleMessageHandler();
            process1.RegisterHandler(_sampleMessageHandler1);

            //This is the process 2, it knows that every message should be directed to queue number 2 and he 
            //listen on queue number 2, he is capable to handle the generic AnotherSampleMessageHandler.
            var mapping2 = new System.Collections.Generic.Dictionary<string, string>()
            {
                { "Jarvis.Framework.Tests" , "jarvistest-input2"} //Command is sent to input2
            };
            TestProcess process2 = new TestProcess(_connectionString, "jarvistest-input2", mapping2);
            var _sampleMessageHandler2 = new AnotherSampleMessageHandler();
            process2.RegisterHandler(_sampleMessageHandler2);

            using (process1)
            using (process2)
            {
                //ok, from the first process I send a message, it should arrive to the second handler not to the first handler.
                await process1.Bus.Send(new AnotherSampleMessage(1));
                AssertOnCondition(() => _sampleMessageHandler2.CallCount == 1, 3, "Process 2 did not handle the first message");

                //First process should not handle the message
                Assert.That(_sampleMessageHandler1.CallCount, Is.EqualTo(0));

                //Now I register myself to the routing
                await process1.Bus.Subscribe<AnotherSampleMessage>();
                await process2.Bus.Subscribe<AnotherSampleMessage>();
                Thread.Sleep(3900);
                await process2.Bus.Send(new AnotherSampleMessage(1));

                //This fail because of the publishing
                AssertOnCondition(() => _sampleMessageHandler2.CallCount == 2, 3, "Process 2 did not handle the second message");
                AssertOnCondition(() => _sampleMessageHandler1.CallCount == 1, 3, "Process 1 handled the second message");
            }
        }

        private void AssertOnCondition(Func<Boolean> condition, Int32 secondsTimeout, String message)
        {
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                if (condition())
                    return;
                Thread.Sleep(100);
            } while (sw.Elapsed.TotalSeconds < secondsTimeout);
            throw new AssertionException(message);
        }

        private class TestProcess : IDisposable
        {
            private readonly IWindsorContainer _container;
            private readonly IBus _bus;
            private readonly BusBootstrapper _busBootstrapper;

            public IBus Bus => _bus;
            public IWindsorContainer Container => _container;

            public TestProcess(String connectionString, String inputQueue, Dictionary<String, String> mapping)
            {
                _container = new WindsorContainer();
                NullMessageTracker tracker = new NullMessageTracker();

                JarvisRebusConfiguration configuration = new JarvisRebusConfiguration(connectionString, "test")
                {
                    ErrorQueue = "jarvistest-errors",
                    InputQueue = inputQueue, //we listen on input1
                    MaxRetry = 3,
                    NumOfWorkers = 1,
                    EndpointsMap = mapping 
                };

                _busBootstrapper = new BusBootstrapper(
                    _container,
                    configuration,
                    tracker);

                _busBootstrapper.Start();
                var rebusConfigurer = _container.Resolve<RebusConfigurer>();
                rebusConfigurer.Start();
                _bus = _container.Resolve<IBus>();
            }

            public void RegisterHandler<T>(IHandleMessages<T> handler)
            {
                _container.Register(
                    Component
                        .For<IHandleMessages<T>>()
                        .Instance(handler));
            }

            public void Dispose()
            {
                _bus.Dispose();
                _busBootstrapper.Stop();
            }
        }

        private void PurgeQueue(String queueName)
        {
            try
            {
                MessageQueue.Delete($".\\Private$\\{queueName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting queue {queueName}: {ex.Message}");
            }
            try
            {
                MessageQueue.Create($".\\Private$\\{queueName}", true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating queue {queueName}: {ex.Message}");
            }
        }
    }
}

#if NETFULL
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Rebus.Support;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Tests.BusTests.Handlers;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using MongoDB.Driver;
using NUnit.Framework;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.BusTests
{
    [NonParallelizable]
    [TestFixture("localconfig")]
    [TestFixture("msmq")]
    [TestFixture("mongodb")]
    public class RebusBaseAssumptionTests
    {
        private String _connectionString;
        private TestProcess process1;
        private TestProcess process2;
        private AnotherSampleMessageHandler handler1;
        private AnotherSampleMessageHandler handler2;
        private readonly string _configurationType;

        public RebusBaseAssumptionTests(string configurationType)
        {
            _configurationType = configurationType;
        }

        private readonly List<AnotherSampleMessageHandler> _processes = new List<AnotherSampleMessageHandler>();

        [SetUp]
        public void SetUp()
        {
            PurgeQueue("jarvistest-input1");
            PurgeQueue("jarvistest-input2");

            _connectionString = ConfigurationManager.ConnectionStrings["rebus"].ConnectionString;
            var url = new MongoUrl(_connectionString);
            var db = new MongoClient(url).GetDatabase(url.DatabaseName);
            db.Drop();

            //Init processes

            //This is the process 1, it knows that every message should be directed to queue number 2 and he 
            //listen on queue number 1, he is capable to handle the generic AnotherSampleMessageHandler.
            var mapping1 = new System.Collections.Generic.Dictionary<string, string>()
            {
                ["Jarvis.Framework.Tests"] = "jarvistest-input2" //Command is sent to input2
            };
            process1 = new TestProcess(_configurationType, _connectionString, "jarvistest-input1", mapping1);
            handler1 = new AnotherSampleMessageHandler();
            _processes.Add(handler1);
            process1.RegisterHandler(handler1);

            var mapping2 = new System.Collections.Generic.Dictionary<string, string>()
            {
                ["Jarvis.Framework.Tests"] = "jarvistest-input2" //Command is sent to input2
            };
            process2 = new TestProcess(_configurationType, _connectionString, "jarvistest-input2", mapping2);
            handler2 = new AnotherSampleMessageHandler();
            _processes.Add(handler2);
            process2.RegisterHandler(handler2);
        }

        [Test]
        public async Task Verify_send_does_not_dispatch_to_subscriber()
        {
            using (process1)
            using (process2)
            {
                //ok, from the first process I send a message, it should arrive to the second handler not to the first handler.
                await process1.Bus.Send(new AnotherSampleMessage(1)).ConfigureAwait(false);

                AssertOnCondition(() => handler2.CallCount == 1, 3, $"{_configurationType} Process 2 did not handle the first message count = { handler2.CallCount} {Dump()}");
                AssertOnCondition(() => handler1.CallCount == 0, 3, $"{_configurationType} Process 1 incorrectly handled the first message");

                //First process should not handle the message
                Assert.That(handler1.CallCount, Is.EqualTo(0));

                //Now I subscribe process2 to Anothersamplemessage.
                await process2.Bus.Subscribe<AnotherSampleMessage>().ConfigureAwait(false);

                //Now send another message, it is a SEND not publish, so it should not be broadcasted.
                await process2.Bus.Send(new AnotherSampleMessage(1)).ConfigureAwait(false);

                AssertOnCondition(() => handler2.CallCount == 2, 3, $"{_configurationType} Process 2 did not handle the second message count = { handler2.CallCount} {Dump()}");
                AssertOnCondition(() => handler1.CallCount == 0, 3, $"{_configurationType} Process 1 incorrectly handled the second message (is a SEND not publish)");

                await process1.Bus.Unsubscribe<AnotherSampleMessage>().ConfigureAwait(false);
                await process2.Bus.Unsubscribe<AnotherSampleMessage>().ConfigureAwait(false);
            }
        }

        private string Dump()
        {
            StringBuilder sb = new StringBuilder("\n");
            foreach (var pinfo in _processes)
            {
                sb.AppendFormat("{0}={1}\n", pinfo.Id, pinfo.CallCount);
            }
            return sb.ToString();
        }

        [Test]
        public async Task Verify_publish_dispatch_to_subscriber()
        {
            using (process1)
            using (process2)
            {
                //this test cannot pass with standard jarvis configuration because it is configured with isCentralized false that is not
                //supported for two queue in the same process.
                if (_configurationType != "localconfig")
                {
                    return;
                }

                //ok, from the first process I PUBLISH a message, it should arrive to the second handler not to the first handler.
                await process1.Bus.Publish(new AnotherSampleMessage(1)).ConfigureAwait(false);

                AssertOnCondition(() => handler1.CallCount == 0, 3, $"{_configurationType} Process 1 incorrectly handled the first message - is a PUBLISH not a SEND");
                AssertOnCondition(() => handler2.CallCount == 0, 3, $"{_configurationType} Process 2 incorrectly handled the first message - is a PUBLISH not a SEND");

                //First process should not handle the message
                Assert.That(handler1.CallCount, Is.EqualTo(0));

                //Now I subscribe process2 to Anothersamplemessage.
                await process2.Bus.Subscribe<AnotherSampleMessage>().ConfigureAwait(false);

                //Now send another message, it is a PUBLISH not publish, so it should not be broadcasted.
                await process2.Bus.Publish(new AnotherSampleMessage(1)).ConfigureAwait(false);

                AssertOnCondition(() => handler1.CallCount == 0, 3, $"{_configurationType} Process 1 incorrectly handled the second message - it was still not subscribed");
                AssertOnCondition(() => handler2.CallCount == 1, 3, $"{_configurationType} Process 2 did not received the second messages, even if it was subscribed");

                //Now I subscribe process1 to Anothersamplemessage.
                await process1.Bus.Subscribe<AnotherSampleMessage>().ConfigureAwait(false);

                //Now I register myself to the routing
                await process2.Bus.Publish(new AnotherSampleMessage(1)).ConfigureAwait(false);

                AssertOnCondition(() => handler1.CallCount == 1, 3, $"{_configurationType} Process 1 did not received the third messages, even if it was subscribed");
                AssertOnCondition(() => handler2.CallCount == 2, 3, $"{_configurationType} Process 2 did not received the third messages, even if it was subscribed");

                await process1.Bus.Unsubscribe<AnotherSampleMessage>().ConfigureAwait(false);
                await process2.Bus.Unsubscribe<AnotherSampleMessage>().ConfigureAwait(false);
            }
        }

        private void AssertOnCondition(Func<Boolean> condition, Int32 secondsTimeout, String message)
        {
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                if (condition())
                {
                    return;
                }

                Thread.Sleep(100);
            } while (sw.Elapsed.TotalSeconds < secondsTimeout);
            throw new AssertionException(message);
        }

#pragma warning disable S3881 // "IDisposable" should be implemented correctly
        private class TestProcess : IDisposable
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
        {
            private readonly IWindsorContainer _container;
            private readonly IBus _bus;
            private readonly BusBootstrapper _busBootstrapper;

            public IBus Bus => _bus;
            public IWindsorContainer Container => _container;

            public TestProcess(string configurationType, String connectionString, String inputQueue, Dictionary<String, String> mapping)
            {
                _container = new WindsorContainer();
                _container.Register(Component.For<Castle.Core.Logging.ILoggerFactory>().Instance(new Castle.Core.Logging.NullLogFactory()));
                NullMessageTracker tracker = new NullMessageTracker();

                MongoUrlBuilder mb = new MongoUrlBuilder(connectionString);

                JarvisRebusConfiguration configuration = new JarvisRebusConfiguration(mb.ToString(), "test")
                {
                    ErrorQueue = "jarvistest-errors",
                    InputQueue = inputQueue, //we listen on input1
                    MaxRetry = 3,
                    NumOfWorkers = 1,
                    EndpointsMap = mapping,
                };

                configuration.CentralizedConfiguration = true;

                configuration.AssembliesWithMessages = new List<System.Reflection.Assembly>()
                {
                    typeof(SampleMessage).Assembly,
                };

                if (configurationType == "msmq")
                {
                    _busBootstrapper = new MsmqTransportJarvisTestBusBootstrapper(
                        _container,
                        configuration,
                        tracker);

                    _busBootstrapper.Start();
                    var rebusConfigurer = _container.Resolve<RebusConfigurer>();
                    rebusConfigurer.Start();
                    _bus = _container.Resolve<IBus>();
                }
                else if (configurationType == "mongodb")
                {
                    _busBootstrapper = new MongoDbTransportJarvisTestBusBootstrapper(
                        _container,
                        configuration,
                        tracker);

                    _busBootstrapper.Start();
                    var rebusConfigurer = _container.Resolve<RebusConfigurer>();
                    rebusConfigurer.Start();
                    _bus = _container.Resolve<IBus>();
                }
                else
                {
                    var mongoUrl = new MongoUrl(connectionString);
                    var mongoClient = new MongoClient(mongoUrl);
                    var _mongoDatabase = mongoClient.GetDatabase(mongoUrl.DatabaseName);

                    var busConfiguration = global::Rebus.Config.Configure.With(new CastleWindsorContainerAdapter(_container))
                        .Logging(l => l.ColoredConsole())
                        .Serialization(c => c.UseNewtonsoftJson(BusBootstrapper.JsonSerializerSettingsForRebus))
                        .Timeouts(t => t.StoreInMongoDb(_mongoDatabase, configuration.Prefix + "-timeouts"))
                        .Subscriptions(s => s.StoreInMongoDb(_mongoDatabase, configuration.Prefix + "-subscriptions", isCentralized: true))
                        .Events(e => e.BeforeMessageSent += BeforeMessageSent);

                    busConfiguration = busConfiguration
                        .Transport(t => t.UseMsmq(configuration.InputQueue))
                        .Options(o => o.SimpleRetryStrategy(
                            errorQueueAddress: configuration.ErrorQueue,
                            maxDeliveryAttempts: configuration.MaxRetry
                         )).Options(o => o.SetNumberOfWorkers(configuration.NumOfWorkers))
                         .Routing(r => r.TypeBased().MapAssemblyOf<AnotherSampleMessage>(mapping.First().Value))
                        ;

                    _bus = busConfiguration.Start();
                }
            }

            private void BeforeMessageSent(IBus bus, Dictionary<string, string> headers, object message, OutgoingStepContext context)
            {
                var attribute = message.GetType()
                           .GetCustomAttributes(typeof(TimeToBeReceivedAttribute), false)
                           .Cast<TimeToBeReceivedAttribute>()
                           .SingleOrDefault();

                if (attribute == null)
                {
                    return;
                }

                headers[Headers.TimeToBeReceived] = attribute.HmsString;
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
                _busBootstrapper?.Stop();
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
#endif
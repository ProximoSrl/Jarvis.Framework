using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Bus.Rebus.Integration.Logging;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Newtonsoft.Json;
using Rebus;
using Rebus.Bus;
using Rebus.Messages;
using Castle.Core;
using Castle.Facilities.Startable;
using Jarvis.Framework.Shared.Helpers;
using Castle.Core.Logging;
using Rebus.Config;
using MongoDB.Driver;
using Rebus.Serialization.Json;
using Jarvis.Framework.Shared.Domain.Serialization;
using Newtonsoft.Json.Serialization;
using Rebus.Pipeline;
using Rebus.Retry;
using Rebus.Transport;
using System.Threading.Tasks;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Routing;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
    public class BusBootstrapper : IStartable
    {
        private readonly IWindsorContainer _container;
        private readonly JarvisRebusConfiguration _configuration;
        private readonly IMongoDatabase _mongoDatabase;
        private readonly IMessagesTracker _messagesTracker;

        public ILogger Logger { get; set; }

        static readonly JsonSerializerSettings JsonSerializerSettingsForRebus = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            ContractResolver = new MessagesContractResolver(),
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            Converters = new JsonConverter[]
            {
                new StringValueJsonConverter()
            }
        };

        public BusBootstrapper(
            IWindsorContainer container,
            JarvisRebusConfiguration configuration,
            IMessagesTracker messagesTracker)
        {
            _container = container;
            _configuration = configuration;
            var mongoUrl = new MongoUrl(configuration.ConnectionString);
            var mongoClient = new MongoClient(mongoUrl);
            _mongoDatabase = mongoClient.GetDatabase(mongoUrl.DatabaseName);
            Logger = NullLogger.Instance;

            // PRXM
            JsonSerializerSettingsForRebus.ContractResolver = new MessagesContractResolver();
            JsonSerializerSettingsForRebus.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
            _messagesTracker = messagesTracker;
            // PRXM
        }

        /// <summary>
        /// This is the method that starts the bus.
        /// </summary>
        public void Start()
        {
            ConfigureAndStart();
        }

        private void ConfigureAndStart()
        {
            var busConfiguration = CreateDefaultBusConfiguration();
            var startableBus = new StartableBus(_container, busConfiguration);
            _container.Register(Component.For<IStartableBus>().Instance(startableBus));
        }

        RebusConfigurer CreateDefaultBusConfiguration()
        {
            Dictionary<String, String> endpointsMap = _configuration.EndpointsMap;
            var router = new JarvisRebusConfigurationManagerRouter(endpointsMap);
            var busConfiguration = global::Rebus.Config.Configure.With(new CastleWindsorContainerAdapter(_container))
                .Logging(l => l.Log4Net())
                .Serialization(c => c.UseNewtonsoftJson(JsonSerializerSettingsForRebus))
                .Timeouts(t => t.StoreInMongoDb(_mongoDatabase, _configuration.Prefix + "-timeouts"))
                .Subscriptions(s => s.StoreInMongoDb(_mongoDatabase, _configuration.Prefix + "-subscriptions"))
                .Events(e => e.BeforeMessageSent += BeforeMessageSent);

            busConfiguration = busConfiguration
                .Options(o => o.Register<IErrorHandler>(c => new JarvisFrameworkErrorHandler(JsonSerializerSettingsForRebus, _container)));

            busConfiguration = busConfiguration
                .Transport(t => t.UseMsmq(_configuration.InputQueue))
                .Options(o => o.SimpleRetryStrategy(
                    errorQueueAddress: _configuration.ErrorQueue, 
                    maxDeliveryAttempts : _configuration.MaxRetry
                 )).Options(o => o.SetNumberOfWorkers(_configuration.NumOfWorkers))
                .Routing(r => r.Register(irc => router));

            return busConfiguration;
        }

        private class JarvisFrameworkErrorHandler : IErrorHandler
        {
            const string JsonContentTypeName = "text/json";
            private readonly JsonSerializerSettings _jsonSerializerSettings;
            private readonly IWindsorContainer _container;

            private readonly Lazy<IBus> _lazyBus;
            private readonly Lazy<IMessagesTracker> _lazyMessageTracker;

            public JarvisFrameworkErrorHandler(
                JsonSerializerSettings jsonSerializerSettings,
               IWindsorContainer container)
            {
                _jsonSerializerSettings = jsonSerializerSettings;
                _container = container;
                _lazyBus = new Lazy<IBus>(() => _container.Resolve<IBus>());
                _lazyMessageTracker = new Lazy<IMessagesTracker>(() => _container.Resolve<IMessagesTracker>());
            }

            public async Task HandlePoisonMessage(TransportMessage transportMessage, ITransactionContext transactionContext, Exception exception)
            {
                if (transportMessage.Headers.ContainsKey("command.id"))
                {
                    Guid commandId = Guid.Parse(transportMessage.Headers["command.id"].ToString());
                    var description = transportMessage.Headers["command.description"].ToString();
                    String exMessage = description;

                    while (exception is TargetInvocationException)
                    {
                        exception = exception.InnerException;
                    }

                    if (exception != null)
                    {
                        exMessage = exception.Message;
                    }

                    var command = GetCommandFromMessage(transportMessage);
                    _lazyMessageTracker.Value.Failed(command, DateTime.UtcNow, exception);

                    if (command != null)
                    {
                        var notifyTo = command.GetContextData(MessagesConstants.ReplyToHeader);

                        if (!string.IsNullOrEmpty(notifyTo))
                        {
                            var commandHandled = new CommandHandled(
                                    notifyTo,
                                    commandId,
                                    CommandHandled.CommandResult.Failed,
                                    description,
                                    exMessage
                                    );

                            commandHandled.CopyHeaders(command);
                            await _lazyBus.Value.Advanced.Routing.Send(
                                transportMessage.Headers["rebus-return-address"],
                               commandHandled).ConfigureAwait(false);
                        }
                    }
                }
            }

            private ICommand GetCommandFromMessage(TransportMessage message)
            {
                var deserializedMessage = Deserialize(message);
                if (deserializedMessage != null && (deserializedMessage.Body as Object[])?.Length > 0)
                {
                    var command = (deserializedMessage.Body as Object[])[0] as ICommand;
                    if (command != null) return command;
                }

                string body;
                switch (message.Headers["rebus-encoding"].ToString().ToLowerInvariant())
                {
                    case "utf-7":
                        body = Encoding.UTF7.GetString(message.Body);
                        break;
                    case "utf-8":
                        body = Encoding.UTF8.GetString(message.Body);
                        break;
                    case "utf-32":
                        body = Encoding.UTF32.GetString(message.Body);
                        break;
                    case "ascii":
                        body = Encoding.ASCII.GetString(message.Body);
                        break;
                    case "unicode":
                        body = Encoding.Unicode.GetString(message.Body);
                        break;
                    default:
                        return null;
                }

                var msg = JsonConvert.DeserializeObject(body, _jsonSerializerSettings);
                var array = msg as Object[];
                if (array != null)
                {
                    return array[0] as ICommand;
                }

                return null;
            }

            private Message Deserialize(TransportMessage transportMessage)
            {
                var headers = new Dictionary<String, String>(transportMessage.Headers);
                var encodingToUse = GetEncodingOrThrow(headers);

                var serializedTransportMessage = encodingToUse.GetString(transportMessage.Body);
                try
                {
                    var messages = (object[])JsonConvert.DeserializeObject(serializedTransportMessage, _jsonSerializerSettings);

                    return new Message(headers, messages);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        string.Format(
                            "An error occurred while attempting to deserialize JSON text '{0}' into an object[]",
                            serializedTransportMessage), e);
                }
            }

            Encoding GetEncodingOrThrow(IDictionary<string, String> headers)
            {
                if (!headers.ContainsKey(Headers.ContentType))
                {
                    throw new ArgumentException(
                        string.Format("Received message does not have a proper '{0}' header defined!",
                                      Headers.ContentType));
                }

                var contentType = (headers[Headers.ContentType] ?? "").ToString();
                if (!JsonContentTypeName.Equals(contentType, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ArgumentException(
                        string.Format(
                            "Received message has content type header with '{0}' which is not supported by the JSON serializer!",
                            contentType));
                }

                if (!headers.ContainsKey(Headers.ContentEncoding))
                {
                    throw new ArgumentException(
                        string.Format(
                            "Received message has content type '{0}', but the corresponding '{1}' header was not present!",
                            contentType, Headers.ContentEncoding));
                }

                var encodingWebName = (headers[Headers.ContentEncoding] ?? "").ToString();

                try
                {
                    return Encoding.GetEncoding(encodingWebName);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        string.Format("An error occurred while attempting to treat '{0}' as a proper text encoding",
                                      encodingWebName), e);
                }
            }
        }

        /// <summary>
        /// http://mookid.dk/oncode/archives/2966
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="headers"></param>
        /// <param name="message"></param>
        /// <param name="context"></param>
        private void BeforeMessageSent(IBus bus, Dictionary<string, string> headers, object message, OutgoingStepContext context)
        {
            if (_messagesTracker != null)
            {
                var msg = message as IMessage;
                if (msg != null)
                {
                    _messagesTracker.Started(msg);
                }
            }

            var attribute = message.GetType()
                       .GetCustomAttributes(typeof(TimeToBeReceivedAttribute), false)
                       .Cast<TimeToBeReceivedAttribute>()
                       .SingleOrDefault();

            if (attribute == null) return;

            headers[Headers.TimeToBeReceived] = attribute.HmsString;
        }

        /// <summary>
        ///   Stops this instance.
        /// </summary>
        public void Stop()
        {
            // Method intentionally left empty.
        }
    }

    internal class JarvisRebusConfigurationManagerRouter : IRouter
    {
        private readonly Dictionary<String, String> _endpointsMap = new Dictionary<String, String>();
        private readonly ConcurrentDictionary<Type, String> _mapCache = new ConcurrentDictionary<Type, string>();

        public JarvisRebusConfigurationManagerRouter(Dictionary<String, String> endpointsMap)
        {
            _endpointsMap = endpointsMap;
            _mapCache = new ConcurrentDictionary<Type, string>();
        }

        public Task<string> GetDestinationAddress(Message message)
        {
            return Task.FromResult<String>(GetEndpointFor(message.Body.GetType()));
        }

        public Task<string> GetOwnerAddress(string topic)
        {
            throw new NotImplementedException("We need to understand if we really need topic");
        }

        public string GetEndpointFor(Type messageType)
        {
            String returnValue = "";
            if (!_mapCache.ContainsKey(messageType))
            {
                //we can have in endpoints configured a namespace or a fully qualified name
                Debug.Assert(messageType != null, "messageType != null");
                var asqn = messageType.FullName + ", " + messageType.Assembly.GetName().Name;
                if (_endpointsMap.ContainsKey(asqn))
                {
                    //exact match
                    returnValue = _endpointsMap[asqn];
                }
                else
                {
                    //find the most specific namespace that contains the type to dispatch.
                    var endpointElement =
                        _endpointsMap
                            .Where(e => asqn.StartsWith(e.Key, StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(e => e.Key.Length)
                            .FirstOrDefault();
                    if (!String.IsNullOrEmpty(endpointElement.Key))
                    {
                        returnValue = endpointElement.Value;
                    }
                }
                _mapCache.TryAdd(messageType, returnValue);
            }
            else
            {
                returnValue = _mapCache[messageType];
            }

            if (!String.IsNullOrEmpty(returnValue)) return returnValue;
            var message = string.Format(@"No endpoint mapping configured for messages of type {0}.

JarvisDetermineMessageOwnershipFromConfigurationManager offers the ability to specify endpoint mappings
in configuration file handled by configuration-manager, you need to configure rebus with endpoints 
section that offer the ability to specify mapping from type to relative endpoint:

 	'pm-rebus' : {{
		'inputQueue' : 'jarvis.ProcessManager.input',
		'errorQueue' : 'jarvis.health',
		'workers' : 2,
		'maxRetries' : 2,
		'endpoints' : [{{
				'messageType' : 'Jarvis.ProcessManager.Shared',
				'endpoint' : 'jarvis.events.input'
			}},
			{{
				'messageType' : 'Jarvis.Dms.Shared',
				'endpoint' : 'jarvis.events.input'
			}}.....

", messageType);
            throw new InvalidOperationException(message);
        }
    }
}

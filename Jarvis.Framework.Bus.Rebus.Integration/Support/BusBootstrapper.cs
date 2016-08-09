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
using Jarvis.Framework.Bus.Rebus.Integration.MongoDb;
using Jarvis.Framework.Bus.Rebus.Integration.Serializers;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Newtonsoft.Json;
using Rebus;
using Rebus.Bus;
using Rebus.Castle.Windsor;
using Rebus.Configuration;
using Rebus.Messages;
using Rebus.Shared;
using Rebus.Transports.Msmq;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
    public class BusBootstrapper
    {
        private readonly IWindsorContainer _container;
        private readonly string _connectionString;
        public string Prefix { get; private set; }

        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            ContractResolver = new MessagesContractResolver(),
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };

        public IMessagesTracker MessagesTracker { get; set; }

        public JarvisRebusConfiguration Configuration { get; set; }

        public CustomJsonSerializer CustomSerializer { get; private set; }

        public Boolean UseCustomSerializer { get; private set; }

        private IStartableBus _bus;

        private Boolean _busStarted = false;

        public BusBootstrapper(
            IWindsorContainer container,
            string connectionString,
            string prefix,
            IMessagesTracker messagesTracker)
        {
            this._container = container;
            this._connectionString = connectionString;
            CustomSerializer = new CustomJsonSerializer();
            Prefix = prefix;
            MessagesTracker = messagesTracker;
        }

        public void StartWithAppConfigWithImmediateStart()
        {
            StartWithAppConfig(true);
        }

        public void StartWithAppConfigWithoutImmediateStart()
        {
            StartWithAppConfig(false);
        }

        public void StartWithAppConfig(Boolean immediateStart)
        {
            if (Configuration == null)
            {
                throw new ConfigurationErrorsException(
@"Configuration property is null!
You probably forgot to register JarvisRebusConfiguration instance in Castle 
or manually set the Configuration property of this instance.");
            }

            var busConfiguration = CreateDefaultBusConfiguration();

            _bus = busConfiguration
                .Transport(t => t.UseMsmqAndGetInputQueueNameFromAppConfig())
                .MessageOwnership(d => d.FromRebusConfigurationSection())
                .CreateBus();

            FixTimeoutHack();

            if (immediateStart) Start(Configuration.NumOfWorkers);
        }

        RebusConfigurer CreateDefaultBusConfiguration()
        {
            var busConfiguration = Configure.With(new WindsorContainerAdapter(_container))
                .Logging(l => l.Log4Net())
                .Serialization(c => c.Use(CustomSerializer))
                .Timeouts(t => t.StoreInMongoDb(_connectionString, Prefix + "-timeouts"))
                .Subscriptions(s => s.StoreInMongoDb(_connectionString, Prefix + "-subscriptions"))
                .Events(e => e.MessageSent += OnMessageSent)
                .Events(e => e.PoisonMessage += OnPoisonMessage)
                .SpecifyOrderOfHandlers(pipeline => pipeline.Use(new RemoveDefaultTimeoutReplyHandlerFilter()));

            UseCustomSerializer = true; //TODO: Make this parametric if needed
            return busConfiguration;
        }

        void FixTimeoutHack()
        {
            _container.Register(Component
                .For<IHandleMessages<TimeoutReply>>()
                .ImplementedBy<CustomTimeoutReplyHandler>()
                );
        }

        public void StartWithConfigurationPropertyWithImmediateStart()
        {
            StartWithConfigurationProperty(true);
        }

        public void StartWithConfigurationPropertyWithoutImmediateStart()
        {
            StartWithConfigurationProperty(false);
        }

        public void StartWithConfigurationProperty(Boolean immediateStart)
        {
            if (Configuration == null)
            {
                throw new ConfigurationErrorsException(
@"Configuration property is null!
You probably forgot to register JarvisRebusConfiguration instance in Castle 
or manually set the Configuration property of this instance.");
            }

            String inputQueueName = Configuration.InputQueue;
            String errorQueueName = Configuration.ErrorQueue;

            Int32 workersNumber = Configuration.NumOfWorkers;

            var busConfiguration = CreateDefaultBusConfiguration();

            //now it is time to load endpoints configuration mapping
            Dictionary<String, String> endpointsMap = Configuration.EndpointsMap;

            _bus = busConfiguration
                .Transport(t => t.UseMsmq(inputQueueName, errorQueueName))
                .MessageOwnership(mo => mo.Use(new JarvisDetermineMessageOwnershipFromConfigurationManager(endpointsMap)))
                .Behavior(b => b.SetMaxRetriesFor<Exception>(Configuration.MaxRetry))
                .CreateBus();

            FixTimeoutHack();

            if (immediateStart) Start(workersNumber);
        }

        public void Start(Int32 numberOfWorkers)
        {
            if (_bus == null)
                throw new ApplicationException("Unable to start, bus still not created.");
            if (_busStarted)
                return;

            _bus.Start(numberOfWorkers);
            _busStarted = true;
        }

        void OnPoisonMessage(IBus bus, ReceivedTransportMessage message, PoisonMessageInfo poisonmessageinfo)
        {
            if (message.Headers.ContainsKey("command.id"))
            {
                Guid commandId = Guid.Parse(message.Headers["command.id"].ToString());
                var description = message.Headers["command.description"].ToString();

                var ex = poisonmessageinfo.Exceptions.Last();
                string exMessage = description;

                Exception exception = ex.Value;
                while (exception is TargetInvocationException)
                {
                    exception = exception.InnerException;
                }

                if (exception != null)
                {
                    exMessage = exception.Message;
                }

                this.MessagesTracker.Failed(commandId, ex.Time, exception);

                var command = GetCommandFromMessage(message);

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
                        bus.Advanced.Routing.Send(
                            message.Headers["rebus-return-address"].ToString(),
                           commandHandled);
                    }
                }
            }
        }

        /// <summary>
        /// http://mookid.dk/oncode/archives/2966
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="destination"></param>
        /// <param name="message"></param>
        private void OnMessageSent(IBus bus, string destination, object message)
        {
            if (MessagesTracker != null)
            {
                var msg = message as IMessage;
                if (msg != null)
                {
                    MessagesTracker.Started(msg);
                }
            }

            var attribute = message.GetType()
                       .GetCustomAttributes(typeof(TimeToBeReceivedAttribute), false)
                       .Cast<TimeToBeReceivedAttribute>()
                       .SingleOrDefault();

            if (attribute == null) return;

            bus.AttachHeader(message, Headers.TimeToBeReceived, attribute.HmsString);
        }

        private ICommand GetCommandFromMessage(ReceivedTransportMessage message)
        {
            if (UseCustomSerializer)
            {
                var deserializedMessage = CustomSerializer.Deserialize(message);
                if (deserializedMessage != null && deserializedMessage.Messages.Length > 0)
                {
                    var command = deserializedMessage.Messages[0] as ICommand;
                    if (command != null) return command;
                }
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
    }

    internal class JarvisDetermineMessageOwnershipFromConfigurationManager : IDetermineMessageOwnership
    {
        private readonly Dictionary<String, String> _endpointsMap = new Dictionary<String, String>();
        private readonly ConcurrentDictionary<Type, String> _mapCache = new ConcurrentDictionary<Type, string>();

        public JarvisDetermineMessageOwnershipFromConfigurationManager(Dictionary<String, String> endpointsMap)
        {
            _endpointsMap = endpointsMap;
            _mapCache = new ConcurrentDictionary<Type, string>();
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

using System;
using System.Collections.Generic;
using System.Linq;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Messages;
using Castle.Core;
using Castle.Core.Logging;
using Rebus.Config;
using MongoDB.Driver;
using Rebus.Serialization.Json;
using Jarvis.Framework.Shared.Domain.Serialization;
using Rebus.Pipeline;
using Rebus.Retry;
using Rebus.Transport;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Support;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
    public abstract partial class BusBootstrapper : IStartable
	{
		private readonly IWindsorContainer _container;
		private readonly IMongoDatabase _mongoDatabase;
		private readonly IMessagesTracker _messagesTracker;

        protected JarvisRebusConfiguration JarvisRebusConfiguration { get; private set; }

        public ILogger Logger { get; set; } = NullLogger.Instance;

		public ILoggerFactory LoggerFactory { get; set; }

		public static readonly JsonSerializerSettings JsonSerializerSettingsForRebus = new JsonSerializerSettings
		{
			TypeNameHandling = TypeNameHandling.All,
			TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
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
			if (configuration == null)
				throw new ArgumentNullException(nameof(configuration));

			if (container == null)
				throw new ArgumentNullException(nameof(container));

			if (messagesTracker == null)
				throw new ArgumentNullException(nameof(messagesTracker));

			if (configuration.AssembliesWithMessages?.Any() == false)
			{
				var message = @"Rebus configuration has no AssembliesWithMessages. It is necessary to 
at least configure one assembly with messages to be dispatched.";
				throw new InvalidOperationException(message);
			}

			_container = container;
            JarvisRebusConfiguration = configuration;
			var mongoUrl = new MongoUrl(configuration.ConnectionString);
            var mongoClient = mongoUrl.CreateClient();

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
			ConfigurationCreated(busConfiguration);
			_container.Register(Component.For<RebusConfigurer>().Instance(busConfiguration));
		}

		/// <summary>
		/// Allows user of Framework to modify rebus configuration, the perfect example is
		/// using specific type of logging.
		/// </summary>
		/// <param name="busConfiguration"></param>
		protected virtual void ConfigurationCreated(RebusConfigurer busConfiguration)
		{
			//do nothing, let the inherited member to configure whatever they want.
		}

		private RebusConfigurer CreateDefaultBusConfiguration()
		{
			var router = new JarvisRebusConfigurationManagerRouterHelper(JarvisRebusConfiguration);

			var busConfiguration = global::Rebus.Config.Configure.With(new CastleWindsorContainerAdapter(_container))
				.Serialization(c => c.UseNewtonsoftJson(JsonSerializerSettingsForRebus))
				.Timeouts(t => t.StoreInMongoDb(_mongoDatabase, JarvisRebusConfiguration.Prefix + "-timeouts"))
				.Subscriptions(s => s.StoreInMongoDb(_mongoDatabase, JarvisRebusConfiguration.Prefix + "-subscriptions", isCentralized: JarvisRebusConfiguration.CentralizedConfiguration))
				.Events(e => e.BeforeMessageSent += BeforeMessageSent);

			var errorHandlerLogger = LoggerFactory?.Create("RebusErrorHandlerLogger") ?? Logger;
			var errorHandler = new JarvisFrameworkErrorHandler(JarvisRebusConfiguration, JsonSerializerSettingsForRebus, _container, errorHandlerLogger);

			//Important, register IErrorHandler before RetryStrategy or it will register default one.
			busConfiguration = busConfiguration
				.Options(o => o.Register<IErrorHandler>(c => errorHandler));

			busConfiguration
				.Options(o =>
				{
					o.SetNumberOfWorkers(JarvisRebusConfiguration.NumOfWorkers);
					o.Decorate<ITransport>(c =>
					{
						var transport = c.Get<ITransport>();
                        JarvisRebusConfiguration.TransportAddress = transport.Address;
						errorHandler.SetTransport(transport);
						return transport;
					});
				});

			busConfiguration = busConfiguration
				//DOTNETCORE does not support msmq, simple let the caller configure everything.
                //.Transport(t => t.UseMsmq(_configuration.InputQueue))
				.Options(o => o.SimpleRetryStrategy(
					errorQueueAddress: JarvisRebusConfiguration.ErrorQueue,
					maxDeliveryAttempts: JarvisRebusConfiguration.MaxRetry
				 ))
				.Routing(r => router.Configure(r.TypeBased()));

			//TODO: REBUS4 - manage configuration.ExplicitSubscriptions
			return busConfiguration;
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
}

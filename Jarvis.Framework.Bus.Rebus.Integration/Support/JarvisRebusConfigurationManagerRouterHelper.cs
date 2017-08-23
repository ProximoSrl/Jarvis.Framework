using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Driver;
using Rebus.Routing.TypeBased;
using System.Configuration;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
	public class JarvisRebusConfigurationManagerRouterHelper
	{
		private readonly ConcurrentDictionary<Type, String> _mapCache = new ConcurrentDictionary<Type, string>();
		private readonly JarvisRebusConfiguration _configuration;

		public JarvisRebusConfigurationManagerRouterHelper(
			JarvisRebusConfiguration configuration)
		{
			_mapCache = new ConcurrentDictionary<Type, string>();
			_configuration = configuration;
		}

		public string GetEndpointFor(Type messageType)
		{
			String returnValue = "";
			if (!_mapCache.ContainsKey(messageType))
			{
				//we can have in endpoints configured a namespace or a fully qualified name
				Debug.Assert(messageType != null, "messageType != null");
				var asqn = messageType.FullName + ", " + messageType.Assembly.GetName().Name;
				if (_configuration.EndpointsMap.ContainsKey(asqn))
				{
					//exact match
					returnValue = _configuration.EndpointsMap[asqn];
				}
				else
				{
					//find the most specific namespace that contains the type to dispatch.
					var endpointElement =
						_configuration.EndpointsMap
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

			return returnValue;
		}

		public void Configure(TypeBasedRouterConfigurationExtensions.TypeBasedRouterConfigurationBuilder typeBasedRouterConfigurationBuilder)
		{
			if (_configuration.AssembliesWithMessages?.Any() != true)
			{
				throw new ConfigurationErrorsException("JarvisRebusConfiguration has no  AssembliesWithMessages configured. This is not permitted because no assembly will be scanned for commands to create routing.");
			}
			foreach (var assembly in _configuration.AssembliesWithMessages)
			{
				var types = assembly.GetTypes();
				var messageTypes = types
					.Where(t => typeof(IMessage).IsAssignableFrom(t) && !t.IsAbstract);

				foreach (var message in messageTypes)
				{
					var endpoint = GetEndpointFor(message);
					if (!String.IsNullOrEmpty(endpoint))
					{
						typeBasedRouterConfigurationBuilder.Map(message, endpoint);
					}
				}
			}
		}
	}
}

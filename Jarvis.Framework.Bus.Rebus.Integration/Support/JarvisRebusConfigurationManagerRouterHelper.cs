using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Driver;
using Rebus.Routing.TypeBased;
using System.Configuration;
using Jarvis.Framework.Shared.Exceptions;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
	public class JarvisRebusConfigurationManagerRouterHelper
	{
		private readonly JarvisRebusConfiguration _configuration;

		public JarvisRebusConfigurationManagerRouterHelper(
			JarvisRebusConfiguration configuration)
		{
			_configuration = configuration;
		}

		public void Configure(TypeBasedRouterConfigurationExtensions.TypeBasedRouterConfigurationBuilder typeBasedRouterConfigurationBuilder)
		{
			if (_configuration.AssembliesWithMessages?.Any() != true)
			{
				throw new JarvisFrameworkEngineException("JarvisRebusConfiguration has no  AssembliesWithMessages configured. This is not permitted because no assembly will be scanned for commands to create routing.");
			}
			foreach (var assembly in _configuration.AssembliesWithMessages)
			{
				var types = assembly.GetTypes();
				var messageTypes = types
					.Where(t => typeof(IMessage).IsAssignableFrom(t) && !t.IsAbstract);

				foreach (var message in messageTypes)
				{
					var endpoint = _configuration.GetEndpointFor(message);
					if (!String.IsNullOrEmpty(endpoint))
					{
						typeBasedRouterConfigurationBuilder.Map(message, endpoint);
					}
				}
			}
		}
	}
}

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

    public class JarvisRebusConfigurationManagerRouter : IRouter
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
            Type type = Type.GetType(topic, false);
            if (type != null)
            {
                return Task.FromResult<String>(GetEndpointFor(type));
            }

            throw new NotSupportedException($"Unable to determine owner of topic {topic}");
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

JarvisRebusConfigurationManagerRouter offers the ability to specify endpoint mappings
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

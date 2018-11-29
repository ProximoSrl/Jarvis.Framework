using Rebus.Bus.Advanced;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Messages.Control;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
	public static class RebusExtension
	{
		/// <summary>
		/// Register a specifc message in a specific queue. 
		/// </summary>
		/// <param name="routingApi"></param>
		/// <param name="address">Address of destination queue, this is the producer of the message</param>
		/// <param name="messageType">Type of message we are interested into</param>
		/// <param name="subscriberAddress">Address of this process, usually taken from <see cref="JarvisRebusConfiguration.TransportAddress"/></param>
		public static Task Subscribe(this IRoutingApi routingApi, String address, Type messageType, String subscriberAddress)
		{
			var subscribeMessage = new SubscribeRequest()
			{
				Topic = messageType.GetSimpleAssemblyQualifiedName(),
				SubscriberAddress = subscriberAddress,
			};
			Dictionary<String, String> headers = new Dictionary<string, string>();
			headers[Headers.Intent] = Headers.IntentOptions.PointToPoint;
			return routingApi.Send(address, subscribeMessage, headers);
		}

        private static readonly ConcurrentDictionary<Type, String> _mapCache = new ConcurrentDictionary<Type, string>();

        /// <summary>
        /// Given a type, this will return destination queue name.
        /// </summary>
        /// <param name="jarvisRebusConfiguration"></param>
        /// <param name="type">This can be a type representing a command or it can also be any other type (like
        /// aggregateId for mixin command)</param>
        /// <returns></returns>
        public static  string GetEndpointFor(this JarvisRebusConfiguration jarvisRebusConfiguration, Type type)
        {
            String returnValue = "";
            if (!_mapCache.ContainsKey(type))
            {
                //we can have in endpoints configured a namespace or a fully qualified name
                var asqn = type.FullName + ", " + type.Assembly.GetName().Name;
                if (jarvisRebusConfiguration.EndpointsMap.ContainsKey(asqn))
                {
                    //exact match
                    returnValue = jarvisRebusConfiguration.EndpointsMap[asqn];
                }
                else
                {
                    //find the most specific namespace that contains the type to dispatch.
                    var endpointElement =
                        jarvisRebusConfiguration.EndpointsMap
                            .Where(e => asqn.StartsWith(e.Key, StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(e => e.Key.Length)
                            .FirstOrDefault();
                    if (!String.IsNullOrEmpty(endpointElement.Key))
                    {
                        returnValue = endpointElement.Value;
                    }
                }
                _mapCache.TryAdd(type, returnValue);
            }
            else
            {
                returnValue = _mapCache[type];
            }

            return returnValue;
        }
    }
}

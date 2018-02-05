using Rebus.Bus.Advanced;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Messages.Control;
using System;
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
	}
}

using Castle.Core;
using Rebus.Bus;
using Rebus.Config;
using System;

namespace Jarvis.Framework.Rebus.Support
{
    public class BusStarter : IStartable
    {
        private readonly RebusConfigurer _rebusConfigurer;
        private IBus _bus;
        private readonly JarvisRebusConfiguration _configuration;

        public BusStarter(
            RebusConfigurer rebusConfigurer,
            JarvisRebusConfiguration configuration)
        {
            _rebusConfigurer = rebusConfigurer;
            _configuration = configuration;
        }

        public void Start()
        {
            if (_bus != null)
            {
                return; //Multiple start.
            }

            _bus = _rebusConfigurer.Start();

            //now register explicit subscriptions.
            foreach (var subscription in _configuration.ExplicitSubscriptions)
            {
                var type = Type.GetType(subscription.MessageType);
                //var subscribeMessage = new SubscribeRequest()
                //{
                //	Topic = type.GetSimpleAssemblyQualifiedName(),
                //	SubscriberAddress = _configuration.TransportAddress,
                //};
                //Dictionary<String, String> headers = new Dictionary<string, string>();
                //headers[Headers.Intent] = Headers.IntentOptions.PointToPoint;
                //_bus.Advanced.Routing.Send(subscription.Endpoint, subscribeMessage, headers);
                _bus.Advanced.Routing.Subscribe(
                    subscription.Endpoint,
                    type,
                    _configuration.TransportAddress).Wait();
            }
        }

        public void Stop()
        {
            _bus.Dispose();
            _bus = null;
        }
    }
}

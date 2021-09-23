#if NETFULL
using Jarvis.Framework.Bus.Rebus.Integration.Support;
using Jarvis.Framework.Shared.Commands.Tracking;
using Rebus.Config;

namespace Jarvis.Framework.Tests.BusTests
{
    internal class MsmqTransportJarvisTestBusBootstrapper : BusBootstrapper
    {
        public MsmqTransportJarvisTestBusBootstrapper(Castle.Windsor.IWindsorContainer container, JarvisRebusConfiguration configuration, IMessagesTracker messagesTracker) : base(container, configuration, messagesTracker)
        {
        }

        protected override void ConfigurationCreated(RebusConfigurer busConfiguration)
        {
            base.ConfigurationCreated(busConfiguration);
            busConfiguration.Timeouts(t => t.StoreInMongoDb(_mongoDatabase, JarvisRebusConfiguration.Prefix + "-timeouts"));
            busConfiguration.Transport(t => t.UseMsmq(JarvisRebusConfiguration.InputQueue));
        }
    }

    internal class MongoDbTransportJarvisTestBusBootstrapper : BusBootstrapper
    {
        public MongoDbTransportJarvisTestBusBootstrapper(Castle.Windsor.IWindsorContainer container, JarvisRebusConfiguration configuration, IMessagesTracker messagesTracker) : base(container, configuration, messagesTracker)
        {
        }

        protected override void ConfigurationCreated(RebusConfigurer busConfiguration)
        {
            busConfiguration.Transport(t => 
                t.UseMongoDb(new MongoDbTransportOptions(JarvisRebusConfiguration.ConnectionString), JarvisRebusConfiguration.InputQueue));
        }
    }
}
#endif

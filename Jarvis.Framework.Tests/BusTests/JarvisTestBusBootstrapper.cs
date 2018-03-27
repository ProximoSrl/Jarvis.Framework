#if NETFULL
using Jarvis.Framework.Bus.Rebus.Integration.Support;
using Rebus.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.BusTests
{
    internal class JarvisTestBusBootstrapper : BusBootstrapper
    {
        public JarvisTestBusBootstrapper(Castle.Windsor.IWindsorContainer container, JarvisRebusConfiguration configuration, Shared.ReadModel.IMessagesTracker messagesTracker) : base(container, configuration, messagesTracker)
        {
        }

        protected override void ConfigurationCreated(RebusConfigurer busConfiguration)
        {
            base.ConfigurationCreated(busConfiguration);
            busConfiguration.Transport(t => t.UseMsmq(JarvisRebusConfiguration.InputQueue));
        }
    }
}
#endif

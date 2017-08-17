using Castle.Core;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Rebus.Bus;
using Rebus.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
    public interface IStartableBus : IStartable
    {
    }

    public class StartableBus : IStartableBus
    {
        private readonly IWindsorContainer _container;
        private readonly RebusConfigurer _rebusConfigurer;
        private IBus _bus;

        public StartableBus(
            IWindsorContainer container,
            RebusConfigurer rebusConfigurer)
        {
            _container = container;
            _rebusConfigurer = rebusConfigurer;
        }
        public void Start()
        {
            if (_bus != null)
                return; //Multiple start.

            _bus = _rebusConfigurer.Start();
        }

        public void Stop()
        {
            _bus.Dispose();
            _bus = null;
        }
    }
}

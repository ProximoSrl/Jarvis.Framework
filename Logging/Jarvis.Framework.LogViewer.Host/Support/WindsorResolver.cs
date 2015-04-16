using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Dependencies;
using Castle.Windsor;

namespace Jarvis.Framework.LogViewer.Host.Support
{
    public class WindsorResolver : IDependencyResolver
    {
        private readonly IWindsorContainer _container;

        public WindsorResolver(IWindsorContainer container)
        {
            _container = container;
        }


        public void Dispose()
        {
            _container.Dispose();
        }

        public object GetService(Type serviceType)
        {

            if (!_container.Kernel.HasComponent(serviceType))
                return null;

            return _container.Resolve(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            if (!_container.Kernel.HasComponent(serviceType))
                return new object[0];

            return _container.ResolveAll(serviceType).Cast<object>();
        }

        public IDependencyScope BeginScope()
        {
            return new WindsorDependencyScope(_container);
        }
    }
}

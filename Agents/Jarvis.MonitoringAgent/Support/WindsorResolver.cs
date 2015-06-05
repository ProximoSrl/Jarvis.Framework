using Castle.MicroKernel;
using Castle.Windsor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Dependencies;

namespace Jarvis.MonitoringAgent.Support
{
    public class WindsorDependencyScope : IDependencyScope
    {

        protected readonly IWindsorContainer _container;
        private ConcurrentBag<object> _toBeReleased = new ConcurrentBag<object>();

        public WindsorDependencyScope(IWindsorContainer container)
        {
            _container = container;
        }

        public void Dispose()
        {
            if (_toBeReleased != null)
            {
                foreach (var o in _toBeReleased)
                {
                    _container.Release(o);
                }
            }
            _toBeReleased = null;
        }

        public object GetService(Type serviceType)
        {
            if (!_container.Kernel.HasComponent(serviceType))
                return null;

            var resolved = _container.Resolve(serviceType);
            if (resolved != null)
                _toBeReleased.Add(resolved);
            return resolved;

        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            if (!_container.Kernel.HasComponent(serviceType))
                return new object[0];


            var allResolved = _container.ResolveAll(serviceType).Cast<object>();
            if (allResolved != null)
            {
                allResolved.ToList()
                    .ForEach(x => _toBeReleased.Add(x));
            }
            return allResolved;

        }
    }

    public class WindsorResolver : System.Web.Http.Dependencies.IDependencyResolver
    {
        private readonly IWindsorContainer _container;

        public WindsorResolver(IWindsorContainer container)
        {
            _container = container;
        }

        public IDependencyScope BeginScope()
        {
            return new WindsorDependencyScope(_container);
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
    }
}

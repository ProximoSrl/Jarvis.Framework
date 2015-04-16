using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Dependencies;
using Castle.Windsor;

namespace Jarvis.Framework.LogViewer.Host.Support
{
    public class WindsorDependencyScope : IDependencyScope
    {
        readonly IWindsorContainer _container;
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

            var allResolved = _container.ResolveAll(serviceType).Cast<object>().ToList();
            allResolved.ForEach(x => _toBeReleased.Add(x));
            return allResolved;

        }
    }
}
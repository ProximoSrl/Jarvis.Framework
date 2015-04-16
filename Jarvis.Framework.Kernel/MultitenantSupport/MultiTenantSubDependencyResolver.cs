using System;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Context;
using Castle.MicroKernel.Resolvers;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;

namespace Jarvis.Framework.Kernel.MultitenantSupport
{
    public class MultiTenantSubDependencyResolver : ISubDependencyResolver
    {
        private TenantManager _tenantManager;
        private IKernel _kernel;
        
        public MultiTenantSubDependencyResolver(IKernel kernel)
        {
            if (kernel == null) throw new ArgumentNullException("kernel");
            _kernel = kernel;
        }

        public bool CanResolve(
            CreationContext context,
            ISubDependencyResolver contextHandlerResolver,
            ComponentModel model,
            DependencyModel dependency)
        {
            if (dependency.TargetType == typeof(ITenant))
                return true;

            if (dependency.TargetType == typeof(IRepositoryEx))
                return true;

            return false;
        }

        public object Resolve(
            CreationContext context,
            ISubDependencyResolver contextHandlerResolver,
            ComponentModel model,
            DependencyModel dependency)
        {
            var tenantId = TenantContext.CurrentTenantId;

            if (dependency.TargetType == typeof (ITenant))
            {
                if (tenantId == null)
                    return NullTenant.Instance;

                if(_tenantManager == null)
                    _tenantManager = _kernel.Resolve<TenantManager>();

                return _tenantManager.GetTenant(tenantId);
            
            }

            if (dependency.TargetType == typeof(IRepositoryEx))
            {
                if(tenantId == null)
                    throw new DependencyResolverException("Cannot create repository without tenant context");

                var repo = _kernel.Resolve<RepositoryEx>(tenantId + ".repository");
                return repo;
            }
            
            return null;
        }
    }
}
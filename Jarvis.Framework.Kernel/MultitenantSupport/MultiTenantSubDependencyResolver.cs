using System;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Context;
using Castle.MicroKernel.Resolvers;
using Jarvis.Framework.Shared.MultitenantSupport;
using NStore.Domain;

namespace Jarvis.Framework.Kernel.MultitenantSupport
{
	public class MultiTenantSubDependencyResolver : ISubDependencyResolver
    {
        private TenantManager _tenantManager;
        private readonly IKernel _kernel;

        public MultiTenantSubDependencyResolver(IKernel kernel)
        {
            if (kernel == null) throw new ArgumentNullException(nameof(kernel));
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

            if (dependency.TargetType == typeof(IRepository))
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

            if (dependency.TargetType == typeof(ITenant))
            {
                if (tenantId == null)
                    return NullTenant.Instance;

                if (_tenantManager == null)
                    _tenantManager = _kernel.Resolve<TenantManager>();

                return _tenantManager.GetTenant(tenantId);
            }

            if (dependency.TargetType == typeof(IRepository))
            {
                if (tenantId == null)
                    throw new DependencyResolverException("Cannot create repository without tenant context");

                var repo = _kernel.Resolve<Repository>(tenantId + ".repository");
                return repo;
            }

            return null;
        }
    }
}
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Jarvis.Framework.Kernel.MultitenantSupport.Exceptions;
using Jarvis.Framework.Shared.MultitenantSupport;
using System;

namespace Jarvis.Framework.Kernel.MultitenantSupport
{
    public class TenantManager : IDisposable, ITenantAccessor
    {
        private readonly IKernel _kernel;
        private readonly object _lockRoot = new object();

        public TenantManager(IKernel kernel)
        {
            _kernel = kernel;
        }

        private string MakeTenantKey(TenantId id)
        {
            return "tenant-" + id;
        }

        public ITenant GetTenant(TenantId id)
        {
            var key = MakeTenantKey(id);
            if (_kernel.HasComponent(key))
                return _kernel.Resolve<ITenant>(key);

            return NullTenant.Instance;
        }

        public ITenant AddTenant(TenantSettings tenantSettings)
        {
            string key = MakeTenantKey(tenantSettings.TenantId);

            lock (_lockRoot)
            {
                if (_kernel.HasComponent(key))
                    throw new TenantAlreadyPresentException(tenantSettings.TenantId);

                var instance = new Tenant(tenantSettings);
                _kernel.Register(
                    Component
                        .For<ITenant>()
                        .Named(key)
                        .Instance(instance)
                );

                return instance;
            }
        }

        public void Dispose()
        {

        }

        public ITenant Current
        {
            get
            {
#if NETFULL
                var tenantId = TenantContext.CurrentTenantId;
                if (tenantId == null)
                    return null;

                return GetTenant(tenantId);
#else
                throw new NotImplementedException("Still not implemented in .NET CORE");
#endif
            }
        }

        //public IEnumerable<ITenant> Tenants {
        //    get { return _kernel.ResolveAll<ITenant>(); }
        //}

        public ITenant[] Tenants
        {
            get { return _kernel.ResolveAll<ITenant>(); }
        }
    }
}
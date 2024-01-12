using System;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.MultitenantSupportTests
{
    [TestFixture]
    public class MultiTenantSubDependencyResolverTests
    {
        private WindsorContainer _container;
        private TenantManager _tenantManager;

        public class Service
        {
            public ITenant Tenant { get; set; }
        }

#pragma warning disable S3881 // "IDisposable" should be implemented correctly
        public class DisposableService : Service, IDisposable
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
        {
            public Boolean Disposed { get; set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _container = new WindsorContainer();
            _container.Register(Component.For<TenantManager>());

            _container.Kernel.Resolver.AddSubResolver(
                new MultiTenantSubDependencyResolver(_container.Kernel)
            );
            _container.Register(Component.For<Service>().LifestyleTransient());
            _container.Register(Component.For<DisposableService>().LifestyleTransient());

            _tenantManager = _container.Resolve<TenantManager>();
        }

        [TearDown]
        public void TearDown()
        {
            TenantContext.Exit();
            _container.Dispose();
        }

        [Test]
        public void resolving_a_component_with_tenant_dependency_outside_a_tenant_context_should_resolve_null_tenant()
        {
            var service = _container.Resolve<Service>();
            NUnit.Framework.Legacy.ClassicAssert.AreSame(NullTenant.Instance, service.Tenant);
        }

        [Test]
        public void resolving_missing_tenant_shuld_inject_null_tenant()
        {
            TenantContext.Enter(new TenantId("not_yet_registered_in_container"));
            var service = _container.Resolve<Service>();
            NUnit.Framework.Legacy.ClassicAssert.AreSame(NullTenant.Instance, service.Tenant);
        }

        [Test]
        public void resolving_registered_tenant_should_inject_correct_tenant()
        {
            var tenantId = new TenantId("tenant");
            var settings = new TenantATestSettings();
            _tenantManager.AddTenant(settings);

            TenantContext.Enter(tenantId);
            var service = _container.Resolve<Service>();

            NUnit.Framework.Legacy.ClassicAssert.AreSame(_tenantManager.GetTenant(tenantId), service.Tenant);
        }

        [Test]
        public void should_resolve_two_tenants()
        {
            var tenant_a = new TenantId("a");
            var tenant_b = new TenantId("b");
            _tenantManager.AddTenant(new TenantATestSettings());
            _tenantManager.AddTenant(new TenantBTestSettings());

            TenantContext.Enter(tenant_a);
            var service_a = _container.Resolve<Service>();
            TenantContext.Enter(tenant_b);
            var service_b = _container.Resolve<Service>();

            NUnit.Framework.Legacy.ClassicAssert.AreSame(_tenantManager.GetTenant(tenant_a), service_a.Tenant);
            NUnit.Framework.Legacy.ClassicAssert.AreSame(_tenantManager.GetTenant(tenant_b), service_b.Tenant);
        }

        [Test]
        public void disposing_disposable_service_should_not_dispose_tenant()
        {
            var tenant_a = new TenantId("a");
            _tenantManager.AddTenant(new TenantATestSettings());
            var tenant = _tenantManager.GetTenant(tenant_a);

            TenantContext.Enter(tenant_a);
            using (var service = _container.Resolve<DisposableService>())
            {
                NUnit.Framework.Legacy.ClassicAssert.AreSame(tenant, service.Tenant);
            }

            NUnit.Framework.Legacy.ClassicAssert.IsFalse(tenant.HasBeedDisposed);
        }
    }
}
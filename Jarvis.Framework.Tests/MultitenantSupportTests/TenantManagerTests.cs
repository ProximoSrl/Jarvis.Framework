using Jarvis.Framework.Tests.EngineTests;

namespace Jarvis.Framework.Tests.MultitenantSupportTests
{
    [TestFixture]
    public class TenantManagerTests
    {
        private TenantManager _manager;
        private readonly TenantId _tenant_a = new TenantId("a");
        private TenantSettings _settings_a;
        private WindsorContainer _container;

        [SetUp]
        public void SetUp()
        {
            _container = new WindsorContainer();
            _manager = new TenantManager(_container.Kernel);
            _settings_a = new TenantATestSettings();
        }

        [TearDown]
        public void TearUp()
        {
            _container.Dispose();
        }

        [Test]
        public void asking_for_an_invalid_tenant_should_return_null()
        {
            var tenant = _manager.GetTenant(new TenantId("not_found"));
            Assert.AreSame(NullTenant.Instance, tenant);
        }

        [Test]
        public void should_be_able_to_add_a_new_tenant()
        {
            _manager.AddTenant(_settings_a);
            var tenant = _manager.GetTenant(_tenant_a);

            Assert.NotNull(tenant);
        }

        [Test]
        public void trying_to_add_a_tenant_twice_should_throw()
        {
            _manager.AddTenant(_settings_a);
            var ex = Assert.Throws<TenantAlreadyPresentException>(() =>
            {
                _manager.AddTenant(_settings_a);
            });

            Assert.AreEqual(_tenant_a, ex.TenantId);
        }
    }
}

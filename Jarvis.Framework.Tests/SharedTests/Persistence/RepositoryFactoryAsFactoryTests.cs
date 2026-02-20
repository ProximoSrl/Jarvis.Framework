using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Shared.Persistence;
using NStore.Domain;
using NSubstitute;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.SharedTests.Persistence
{
    /// <summary>
    /// Tests to verify that IRepositoryFactory can be used with Castle Windsor's
    /// TypedFactoryFacility (AsFactory) to create and release repositories.
    /// </summary>
    [TestFixture]
    public class RepositoryFactoryAsFactoryTests
    {
        private WindsorContainer _container;
        private IRepository _mockRepository;
        private IBatchRepository _mockBatchRepository;

        [SetUp]
        public void Setup()
        {
            _container = new WindsorContainer();
            _container.AddFacility<TypedFactoryFacility>();

            _mockRepository = Substitute.For<IRepository>();
            _mockBatchRepository = Substitute.For<IBatchRepository>();

            // Register the factory interface with AsFactory
            _container.Register(Component.For<IRepositoryFactory>().AsFactory());

            // Register concrete implementations for IRepository
            _container.Register(Component
                .For<IRepository>()
                .UsingFactoryMethod(() => _mockRepository)
                .LifestyleTransient());

            // Register concrete implementations for IBatchRepository
            _container.Register(Component
                .For<IBatchRepository>()
                .UsingFactoryMethod(() => _mockBatchRepository)
                .LifestyleTransient());
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
        }

        [Test]
        public void Can_resolve_IRepositoryFactory_from_container()
        {
            var factory = _container.Resolve<IRepositoryFactory>();

            Assert.That(factory, Is.Not.Null);
        }

        [Test]
        public void Create_returns_IRepository_instance()
        {
            var factory = _container.Resolve<IRepositoryFactory>();

            var repository = factory.Create();

            Assert.That(repository, Is.Not.Null);
            Assert.That(repository, Is.SameAs(_mockRepository));
        }

        [Test]
        public void CreateBatch_returns_IBatchRepository_instance()
        {
            var factory = _container.Resolve<IRepositoryFactory>();

            var batchRepository = factory.CreateBatch();

            Assert.That(batchRepository, Is.Not.Null);
            Assert.That(batchRepository, Is.SameAs(_mockBatchRepository));
        }

        [Test]
        public void Release_IRepository_does_not_throw()
        {
            var factory = _container.Resolve<IRepositoryFactory>();
            var repository = factory.Create();

            Assert.DoesNotThrow(() => factory.Release(repository));
        }

        [Test]
        public void Release_IBatchRepository_does_not_throw()
        {
            var factory = _container.Resolve<IRepositoryFactory>();
            var batchRepository = factory.CreateBatch();

            Assert.DoesNotThrow(() => factory.Release(batchRepository));
        }

        [Test]
        public void Can_create_multiple_IRepository_instances()
        {
            var factory = _container.Resolve<IRepositoryFactory>();

            var repository1 = factory.Create();
            var repository2 = factory.Create();

            Assert.That(repository1, Is.Not.Null);
            Assert.That(repository2, Is.Not.Null);
        }

        [Test]
        public void Can_create_both_IRepository_and_IBatchRepository_from_same_factory()
        {
            var factory = _container.Resolve<IRepositoryFactory>();

            var repository = factory.Create();
            var batchRepository = factory.CreateBatch();

            Assert.That(repository, Is.Not.Null);
            Assert.That(batchRepository, Is.Not.Null);
            Assert.That(repository, Is.SameAs(_mockRepository));
            Assert.That(batchRepository, Is.SameAs(_mockBatchRepository));
        }

        [Test]
        public void Factory_is_proxy_generated_by_Castle()
        {
            var factory = _container.Resolve<IRepositoryFactory>();

            // Verify it's a Castle proxy
            Assert.That(factory.GetType().FullName, Does.Contain("Castle.Proxies"));
        }
    }
}

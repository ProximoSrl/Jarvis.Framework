using Castle.Facilities.TypedFactory;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Tests.SharedTests.IdentitySupport;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class CollectionWrapperResolveWithTypedFacilityTests
    {
        private WindsorContainer _container;

        [SetUp]
        public void Setup()
        {
            _container = new WindsorContainer();
            _container.AddFacility<TypedFactoryFacility>();
            _container.Register(
                Component.For<IMongoStorageFactory>().Instance(new NullMongoStorageFactory()),
                Component.For<INotifyToSubscribers>().ImplementedBy<NotifyToNobody>(),
                Component.For(new[]
                   {
                        typeof (ICollectionWrapper<,>),
                        typeof (IReadOnlyCollectionWrapper<,>)
                   })
                   .ImplementedBy(typeof(CollectionWrapper<,>))
                   );
        }

        [TearDown]
        public void Teardown()
        {
            if (_container != null)
                _container.Dispose();
        }

        [Test]
        public void ResolveCollectionWrapper_using_TypedFactoryFacility_Failure()
        {
            var c = _container.Resolve<ICollectionWrapper<SampleReadModelTestId, TestId>>();
            Assert.IsNotNull(c);
            Assert.That(c.TransformForNotification.Target.ToString().Contains("Castle.Proxies"));
            Assert.Throws<ComponentNotFoundException>(() =>
            {
                c.TransformForNotification(null);
            });
        }
    }

    [TestFixture]
    public class CollectionWrapperResolveWithJarvisTypedFacilityTests
    {
        private WindsorContainer _container;

        [SetUp]
        public void Setup()
        {
            _container = new WindsorContainer();
            //_container.AddFacility<TypedFactoryFacility>();
            _container.AddFacility<JarvisTypedFactoryFacility>();
            _container.Register(
                Component.For<IMongoStorageFactory>().Instance(new NullMongoStorageFactory()),
                Component.For<INotifyToSubscribers>().ImplementedBy<NotifyToNobody>(),
                Component.For(new[]
                   {
                        typeof (ICollectionWrapper<,>),
                        typeof (IReadOnlyCollectionWrapper<,>)
                   })
                   .ImplementedBy(typeof(CollectionWrapper<,>))
                   );
        }

        [TearDown]
        public void Teardown()
        {
            if (_container != null)
                _container.Dispose();
        }

        [Test]
        public void ResolveCollectionWrapper_using_JarvisTypedFactoryFacility_Success()
        {
            var c = _container.Resolve<ICollectionWrapper<SampleReadModelTestId, TestId>>();
            Assert.IsNotNull(c);
            Assert.That(!c.TransformForNotification.Target.ToString().Contains("Castle.Proxies"));
            Assert.DoesNotThrow(() =>
            {
                c.TransformForNotification(null);
            });
        }
    }

    public class NullMongoStorageFactory : IMongoStorageFactory
    {
        IMongoStorage<TModel, TKey> IMongoStorageFactory.GetCollection<TModel, TKey>()
        {
            return null;
        }
    }
}

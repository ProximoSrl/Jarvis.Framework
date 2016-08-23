using Castle.Core;
using Castle.Facilities.Startable;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.TestHelpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.SharedTests.Helpers
{
    [TestFixture]
    public class CastleUtilsTests
    {

        [Test]
        public void Startable_work_with_interface()
        {
            var startToken = token;
            using (var sut = new WindsorContainer())
            {
                var startableFacility = new JarvisStartableFacility(new TestLogger());
                sut.AddFacility(startableFacility);

                sut.Register(Component.For<IStartable>().ImplementedBy<TestStartable>());

                startableFacility.StartAllIStartable();
                Assert.That(token, Is.EqualTo(startToken + 1));//one component started.
            }
            Assert.That(token, Is.EqualTo(startToken));//stop called
        }


        [Test]
        public void Startable_work_with_concrete_class()
        {
            var startToken = token;
            using (var sut = new WindsorContainer())
            {
                var startableFacility = new JarvisStartableFacility(new TestLogger());
                sut.AddFacility(startableFacility);

                sut.Register(Component.For<TestStartable>());

                startableFacility.StartAllIStartable();
                Assert.That(token, Is.EqualTo(startToken + 1));//one component started.
            }
            Assert.That(token, Is.EqualTo(startToken));//stop called
        }

        [Test]
        public void Startable_work_with_method()
        {
            var startToken = token;
            using (var sut = new WindsorContainer())
            {
                var startableFacility = new JarvisStartableFacility(new TestLogger());
                sut.AddFacility(startableFacility);

                sut.Register(Component
                    .For<TestStartableMethod>()
                    .StartUsingMethod(t => t.StartMethod)
                    .StopUsingMethod(t => t.StopMethod));

                startableFacility.StartAllIStartable();
                Assert.That(token, Is.EqualTo(startToken + 1));//one component started.
            }
            Assert.That(token, Is.EqualTo(startToken));//stop called
        }

        [Test]
        public void priority_handling()
        {
            var startToken = token;
            using (var sut = new WindsorContainer())
            {
                var startableFacility = new JarvisStartableFacility(new TestLogger());
                sut.AddFacility(startableFacility);

                sut.Register(Component
                    .For<TestStartableMethod>()
                    .StartUsingMethod(t => t.StartMethod)
                    .StopUsingMethod(t => t.StopMethod));

                sut.Register(Component
                    .For<TestStartable>()
                    .WithStartablePriorityHighest());

                startableFacility.StartAllIStartable();
                Assert.That(token, Is.EqualTo(startToken + 2));//two component started.

                var first = sut.Resolve<TestStartable>();
                Assert.That(first.StartToken, Is.EqualTo(startToken + 1));
                var second = sut.Resolve<TestStartableMethod>();
                Assert.That(second.StartToken, Is.EqualTo(startToken + 2));
            }
            Assert.That(token, Is.EqualTo(startToken));//stop called
        }

        [Test]
        public void priority_handling_lowest()
        {
            var startToken = token;
            using (var sut = new WindsorContainer())
            {
                var startableFacility = new JarvisStartableFacility(new TestLogger());
                sut.AddFacility(startableFacility);

                sut.Register(Component
                    .For<TestStartableMethod>()
                    .StartUsingMethod(t => t.StartMethod)
                    .StopUsingMethod(t => t.StopMethod)
                    .WithStartablePriorityLowest());

                sut.Register(Component.For<TestStartable>());

                startableFacility.StartAllIStartable();
                Assert.That(token, Is.EqualTo(startToken + 2));//two component started.

                var first = sut.Resolve<TestStartable>();
                Assert.That(first.StartToken, Is.EqualTo(startToken + 1));
                var second = sut.Resolve<TestStartableMethod>();
                Assert.That(second.StartToken, Is.EqualTo(startToken + 2));
            }
            Assert.That(token, Is.EqualTo(startToken));//stop called
        }

        private static Int32 token = 0;

        public class TestStartable : IStartable
        {
            public Int32? StartToken { get; set; }

            public void Start()
            {
                StartToken = Interlocked.Increment(ref token);
            }

            public void Stop()
            {
                StartToken = null;
                Interlocked.Decrement(ref token);
            }
        }

        public class TestStartableMethod
        {
            public Int32? StartToken { get; set; }

            public void StartMethod()
            {
                StartToken = Interlocked.Increment(ref token);
            }

            public void StopMethod()
            {
                StartToken = null;
                Interlocked.Decrement(ref token);
            }
        }
    }
}

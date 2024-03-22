using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicReadmodelFactoryTests : AtomicProjectionEngineTestBase
    {
        [Test]
        public void Can_simply_create()
        {
            var sut = new AtomicReadModelFactory();
            var rm = sut.Create<SimpleTestAtomicReadModel>("SampleAggregate_234");
            Assert.That(rm.Id, Is.EqualTo(new SampleAggregateId(234).AsString()));
        }

        /// <summary>
        /// Verify the ability to create a readmodel, then override the base factory
        /// created by the <see cref="AtomicReadModelFactory"/> with a custom one
        /// registered after the first one.
        /// </summary>
        [Test]
        public void Can_override_factory()
        {
            var sut = new AtomicReadModelFactory();
            var rm = sut.Create<SimpleTestAtomicReadModel>("SampleAggregate_234");
            int count = 0;
            sut.AddFactory<SimpleTestAtomicReadModel>(id => { count++; return new SimpleTestAtomicReadModel(id); });

            var rm2 = sut.Create<SimpleTestAtomicReadModel>("SampleAggregate_234");
            Assert.That(rm2.Id, Is.EqualTo(new SampleAggregateId(234).AsString()));
            Assert.That(count, Is.EqualTo(1));
        }
    }
}

using Jarvis.Framework.Tests.EngineTests;

namespace Jarvis.Framework.Tests.Kernel.Support
{
    [TestFixture]
    public class IdentityToAggregateManagerTests
    {
        private IdentityToAggregateManager _sut;

        [SetUp]
        public void SetUp()
        {
            _sut = new IdentityToAggregateManager();
        }

        [Test]
        public void Verify_basic_scan()
        {
            _sut.ScanAssemblyForAggregateRoots(Assembly.GetExecutingAssembly());
            var type = _sut.GetAggregateFromId(new SampleAggregateId(1));
            Assert.That(type, Is.EqualTo(typeof(SampleAggregate)));
        }
    }
}

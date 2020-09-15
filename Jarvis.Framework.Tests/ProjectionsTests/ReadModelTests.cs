namespace Jarvis.Framework.Tests.ProjectionsTests
{
    [TestFixture]
    public class ReadModelTests
    {
        private class SampleReadModelWithStringKey : AbstractReadModel<string>
        {
        }

        class SampleReadModelWithIntKey : AbstractReadModel<int>
        {
        }

        class SampleReadModelWithGuidKey : AbstractReadModel<Guid>
        {
        }

        [Test]
        public void test_string()
        {
            var ex = Assert.Throws<InvalidAggregateIdException>(() => new SampleReadModelWithStringKey().ThrowIfInvalidId());
            Assert.AreEqual("Empty Id Value", ex.Message);
        }

        [Test]
        public void test_guid()
        {
            var ex = Assert.Throws<InvalidAggregateIdException>(() => new SampleReadModelWithGuidKey().ThrowIfInvalidId());
            Assert.AreEqual("Empty Id Value", ex.Message);
        }

        [Test]
        public void test_int()
        {
            var ex = Assert.Throws<InvalidAggregateIdException>(() => new SampleReadModelWithIntKey().ThrowIfInvalidId());
            Assert.AreEqual("Empty Id Value", ex.Message);
        }
    }
}
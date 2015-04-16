using System;
using Jarvis.Framework.Shared.ReadModel;
using NUnit.Framework;

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
            var ex = Assert.Throws<Exception>(() => new SampleReadModelWithStringKey().ThrowIfInvalidId());
            Assert.AreEqual("Empty Id Value", ex.Message);
        }

        [Test]
        public void test_guid()
        {
            var ex = Assert.Throws<Exception>(() => new SampleReadModelWithGuidKey().ThrowIfInvalidId());
            Assert.AreEqual("Empty Id Value", ex.Message);
        }

        [Test]
        public void test_int()
        {
            var ex = Assert.Throws<Exception>(() => new SampleReadModelWithIntKey().ThrowIfInvalidId());
            Assert.AreEqual("Empty Id Value", ex.Message);
        }
    }
}
using System;
using Jarvis.Framework.Shared.Exceptions;
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
            var ex = Assert.Throws<InvalidAggregateIdException>(() => new SampleReadModelWithStringKey().ThrowIfInvalidId());
            NUnit.Framework.Legacy.ClassicAssert.AreEqual("Empty Id Value", ex.Message);
        }

        [Test]
        public void test_guid()
        {
            var ex = Assert.Throws<InvalidAggregateIdException>(() => new SampleReadModelWithGuidKey().ThrowIfInvalidId());
            NUnit.Framework.Legacy.ClassicAssert.AreEqual("Empty Id Value", ex.Message);
        }

        [Test]
        public void test_int()
        {
            var ex = Assert.Throws<InvalidAggregateIdException>(() => new SampleReadModelWithIntKey().ThrowIfInvalidId());
            NUnit.Framework.Legacy.ClassicAssert.AreEqual("Empty Id Value", ex.Message);
        }
    }
}
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;
using System;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    [TestFixture]
    public class EventStoreIdentityTests
    {
        [TestCase("SampleAggregate_3")]
        [TestCase("SampleAggregate_32")]
        [TestCase("SampleAggregate_45645674756756867")]
        public void Successful_match_correct_identities(String id)
        {
            Assert.That(EventStoreIdentity.Match<SampleAggregateId>(id));
        }

        [TestCase("SampleAggregate_")]
        [TestCase("SampleAggregate 3")]
        [TestCase("SampleAggregate-3")]
        [TestCase("SampleAggregate")]
        [TestCase("SampleAggregate324")]
        [TestCase("SampleAggregate_-2")]
        [TestCase("SampleAggregate_324A")]
        [TestCase("SampleAggregateId_34")]
        [TestCase("SampleAggregate_asdfdasfdas")]
        [TestCase("AtomicAggregate_34")]
        [TestCase(null)]
        [TestCase("")]
        public void Successful_detect_wrong_identities(String id)
        {
            Assert.That(EventStoreIdentity.Match<SampleAggregateId>(id), Is.False);
        }

        [Test]
        public void Verify_exception_when_incorrect_id_is_used()
        {
            try
            {
                new SampleAggregateId("Document_3");
            }
            catch (JarvisFrameworkIdentityException ie)
            {
                Assert.That(ie.Message, Contains.Substring("Document tag is not valid for type"));
                Assert.That(ie.Message, Contains.Substring("Tag expected: SampleAggregate"));
            }
        }

        [Test]
        public void Verify_equality_base() 
        {
            var id1 = new SampleAggregateId(42);
            var id2 = new SampleAggregateId(42);
            Assert.That(id1.Equals(id2));
            Assert.That(id1 == id2);
            Assert.That(object.Equals(id1, id2));
        }

        [Test]
        public void Verify_equality_through_interface()
        {
            IIdentity id1 = (IIdentity) new SampleAggregateId(42);
            IIdentity id2 = (IIdentity) new SampleAggregateId(42);
            Assert.That(id1.Equals(id2));
            Assert.That(object.Equals(id1, id2));
        }

        [TestCase("")]
        [TestCase(null)]
        public void Argument_null_exception_is_throw(string data)
        {
            Assert.Throws<ArgumentNullException>(() => new SampleAggregateId(data));
        }
    }
}

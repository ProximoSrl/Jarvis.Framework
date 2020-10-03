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
    }
}

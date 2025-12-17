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

        [TestCase("sampleAggregate_3")]
        [TestCase("Sampleaggregate_32")]
        [TestCase("sampleaggregate_45645674756756867")]
        public void Case_insensitive_match(String id)
        {
            Assert.That(EventStoreIdentity.Match<SampleAggregateId>(id));
        }

        [TestCase("sampleAggregate_3")]
        [TestCase("Sampleaggregate_32")]
        [TestCase("sampleaggregate_45645674756756867")]
        [TestCase("12")] //Special case, create with a string that represent a valid number.
        public void Case_insensitive_and_special_case_creation(String id)
        {
            Assert.That(new SampleAggregateId(id) is SampleAggregateId);
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
        [TestCase("_12")]
        public void Successful_detect_wrong_identities(String id)
        {
            Assert.That(EventStoreIdentity.Match<SampleAggregateId>(id), Is.False);
        }

        [TestCase("SampleAggregate_", typeof(JarvisFrameworkIdentityException))]
        [TestCase("SampleAggregate 3", typeof(JarvisFrameworkIdentityException))]
        [TestCase("SampleAggregate-3", typeof(JarvisFrameworkIdentityException))]
        [TestCase("SampleAggregate", typeof(JarvisFrameworkIdentityException))]
        [TestCase("SampleAggregate324", typeof(JarvisFrameworkIdentityException))]
        [TestCase("SampleAggregate_-2", typeof(JarvisFrameworkIdentityException))]
        [TestCase("SampleAggregate_324A", typeof(JarvisFrameworkIdentityException))]
        [TestCase("SampleAggregate__32", typeof(JarvisFrameworkIdentityException))]
        [TestCase("SampleAggregateId_34", typeof(JarvisFrameworkIdentityException))]
        [TestCase("SampleAggregate_asdfdasfdas", typeof(JarvisFrameworkIdentityException))]
        [TestCase("AtomicAggregate_34", typeof(JarvisFrameworkIdentityException))]
        [TestCase(null, typeof(ArgumentNullException))]
        [TestCase("", typeof(ArgumentNullException))]
        [TestCase("_12", typeof(JarvisFrameworkIdentityException))]
        [TestCase("-12", typeof(JarvisFrameworkIdentityException))]
        public void Invalid_id_throws(String id, Type expectedException)
        {
            Assert.That(EventStoreIdentity.Match<SampleAggregateId>(id), Is.False);
            Assert.Throws(expectedException, () => new SampleAggregateId(id));
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
            IIdentity id1 = (IIdentity)new SampleAggregateId(42);
            IIdentity id2 = (IIdentity)new SampleAggregateId(42);
            Assert.That(id1.Equals(id2));
            Assert.That(object.Equals(id1, id2));
        }

        [TestCase("")]
        [TestCase(null)]
        public void Argument_null_exception_is_throw(string data)
        {
            Assert.Throws<ArgumentNullException>(() => new SampleAggregateId(data));
        }

        [Test]
        public void Verify_hashcode_is_case_insensitive()
        {
            var id1 = new SampleAggregateId("SampleAggregate_42");
            var id2 = new SampleAggregateId("sampleaggregate_42");

            Assert.That(id1.AsString(), Is.EqualTo(id2.AsString()));
            Assert.That(id1.GetHashCode(), Is.EqualTo(id2.GetHashCode()));
        }

        [Test]
        public void Verify_dictionary_usage_with_case_insensitive_ids()
        {
            var dict = new System.Collections.Generic.Dictionary<SampleAggregateId, string>();
            var id1 = new SampleAggregateId("SampleAggregate_100");
            var id2 = new SampleAggregateId("sampleaggregate_100");

            dict[id1] = "test";

            Assert.That(dict.ContainsKey(id2), Is.True, "Dictionary should contain key created with different casing");
            Assert.That(dict[id2], Is.EqualTo("test"));
        }
    }
}

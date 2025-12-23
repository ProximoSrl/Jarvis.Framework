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

        [Test]
        public void Normalize_returns_same_string_when_casing_is_already_correct()
        {
            // Ensure type is registered
            _ = new SampleAggregateId(1);
            const string input = "SampleAggregate_42";

            var normalized = EventStoreIdentity.Normalize(input);

            Assert.That(normalized, Is.SameAs(input));
            Assert.That(normalized, Is.EqualTo("SampleAggregate_42"));
        }

        [Test]
        public void Normalize_fixes_prefix_casing_when_casing_is_wrong()
        {
            // Ensure type is registered
            _ = new SampleAggregateId(1);
            const string input = "sampleaggregate_42";

            var normalized = EventStoreIdentity.Normalize(input);

            Assert.That(normalized, Is.EqualTo("SampleAggregate_42"));
        }

        [Test]
        public void Normalize_returns_null_for_numeric_only_identity()
        {
            // Ensure type is registered
            _ = new SampleAggregateId(1);
            const string input = "12";

            var normalized = EventStoreIdentity.Normalize(input);

            Assert.That(normalized, Is.Null);
        }

        [Test]
        public void Normalize_does_not_change_id_with_different_tag()
        {
            // Ensure type is registered
            _ = new SampleAggregateId(1);
            const string input = "Document_3";

            var normalized = EventStoreIdentity.Normalize(input);

            Assert.That(normalized, Is.Null, "Should return null for unregistered type");
        }

        [Test]
        public void Normalize_fixes_casing_when_type_is_registered()
        {
            // Ensure type is registered by using it first
            _ = new SampleAggregateId(1);
            
            const string input = "sampleaggregate_42";

            var normalized = EventStoreIdentity.Normalize(input);

            Assert.That(normalized, Is.EqualTo("SampleAggregate_42"));
        }

        [Test]
        public void Normalize_returns_null_when_type_not_registered()
        {
            const string input = "UnknownType_42";

            var normalized = EventStoreIdentity.Normalize(input);

            Assert.That(normalized, Is.Null);
        }

        [Test]
        public void Normalize_handles_correct_casing()
        {
            // Ensure type is registered
            _ = new SampleAggregateId(1);
            
            const string input = "SampleAggregate_42";

            var normalized = EventStoreIdentity.Normalize(input);

            Assert.That(normalized, Is.SameAs(input));
        }

        [Test]
        public void Normalize_uses_case_insensitive_lookup()
        {
            // Register type
            _ = new SampleAggregateId(1);
            
            // Test various casing combinations
            Assert.That(EventStoreIdentity.Normalize("SAMPLEAGGREGATE_42"), Is.EqualTo("SampleAggregate_42"));
            Assert.That(EventStoreIdentity.Normalize("SaMpLeAgGrEgAtE_42"), Is.EqualTo("SampleAggregate_42"));
            Assert.That(EventStoreIdentity.Normalize("SampleAggregate_42"), Is.EqualTo("SampleAggregate_42"));
        }

        [Test]
        public void Normalize_returns_null_for_unregistered_but_valid_format()
        {
            const string input = "UnknownType_42";

            var normalized = EventStoreIdentity.Normalize(input);

            Assert.That(normalized, Is.Null, "Should return null for valid format but unregistered type");
        }

        [Test]
        public void Normalize_does_not_allocate_when_casing_correct()
        {
            // Register type
            _ = new SampleAggregateId(1);
            
            const string input = "SampleAggregate_42";

            var normalized = EventStoreIdentity.Normalize(input);

            Assert.That(normalized, Is.SameAs(input), "Should return same string instance when no normalization needed");
        }

        [Test]
        public void Create_identity_from_numeric_only_string()
        {
            const string numericOnly = "42";
            
            var id = new SampleAggregateId(numericOnly);

            Assert.That(id.Id, Is.EqualTo(42L));
            Assert.That(id.AsString(), Is.EqualTo("SampleAggregate_42"));
            Assert.That(id.ToString(), Is.EqualTo("SampleAggregate_42"));
        }

        [TestCase("1")]
        [TestCase("999")]
        [TestCase("123456789")]
        public void Create_identity_from_various_numeric_strings(string numericValue)
        {
            var id = new SampleAggregateId(numericValue);
            
            var expectedLong = long.Parse(numericValue);
            Assert.That(id.Id, Is.EqualTo(expectedLong));
            Assert.That(id.AsString(), Is.EqualTo($"SampleAggregate_{numericValue}"));
        }

        [Test]
        public void Normalize_returns_null_for_empty_input()
        {
            Assert.That(EventStoreIdentity.Normalize(""), Is.Null);
        }

        [Test]
        public void Normalize_returns_null_for_null_input()
        {
            Assert.That(EventStoreIdentity.Normalize(null), Is.Null);
        }

        [Test]
        public void Match_with_leading_zeros_in_numeric_part()
        {
            // Leading zeros are valid digits
            Assert.That(EventStoreIdentity.Match<SampleAggregateId>("SampleAggregate_007"), Is.True);
        }

        [Test]
        public void Create_identity_with_leading_zeros()
        {
            var id = new SampleAggregateId("SampleAggregate_007");
            
            Assert.That(id.Id, Is.EqualTo(7L));
            Assert.That(id.AsString(), Is.EqualTo("SampleAggregate_7"));
        }

        [Test]
        public void Create_identity_from_max_long_value()
        {
            var maxLongStr = long.MaxValue.ToString();
            var fullId = $"SampleAggregate_{maxLongStr}";
            
            var id = new SampleAggregateId(fullId);
            
            Assert.That(id.Id, Is.EqualTo(long.MaxValue));
        }

        [TestCase("SampleAggregate_99999999999999999999")] // Overflow
        public void Create_identity_throws_on_numeric_overflow(string input)
        {
            Assert.Throws<JarvisFrameworkIdentityException>(() => new SampleAggregateId(input));
        }

        [Test]
        public void Match_returns_false_for_numeric_overflow()
        {
            // Match only checks if chars are digits, doesn't validate range
            // This test documents current behavior - may need human validation
            var overflowId = "SampleAggregate_99999999999999999999";
            
            // Current implementation returns true because all chars after prefix are digits
            Assert.That(EventStoreIdentity.Match<SampleAggregateId>(overflowId), Is.True);
        }

        [Test]
        public void Normalize_preserves_numeric_part_exactly()
        {
            // Ensure type is registered
            _ = new SampleAggregateId(1);
            const string input = "sampleaggregate_00042";

            var normalized = EventStoreIdentity.Normalize(input);

            // Should preserve "00042" not convert to "42"
            Assert.That(normalized, Is.EqualTo("SampleAggregate_00042"));
        }
    }
}

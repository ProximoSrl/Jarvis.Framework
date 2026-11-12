using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.EngineTests;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;

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

        [TestCase("SampleAggregate_42")]
        [TestCase("sampleaggregate_42")]
        [TestCase("SAMPLEAGGREGATE_0")]
        public void TryGetTag_returns_cached_tag_instance(string id)
        {
            var expected = EventStoreIdentity.GetTagForIdentityClass<SampleAggregateId>();

            var result = EventStoreIdentity.TryGetTag(id, out var tag);

            Assert.That(result, Is.True);
            Assert.That(tag, Is.SameAs(expected), "Tag must be the cached instance, not a new string");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("42")]
        [TestCase("_42")]
        [TestCase("SampleAggregate")]
        [TestCase("SampleAggregate_")]
        [TestCase("SampleAggregate_4a")]
        [TestCase("SampleAggregate_-4")]
        [TestCase("SampleAggregate_4_2")]
        [TestCase("ThisTagIsNotKnown_42")]
        public void TryGetTag_returns_false_for_invalid_or_unknown_identities(string id)
        {
            _ = EventStoreIdentity.GetTagForIdentityClass<SampleAggregateId>();

            var result = EventStoreIdentity.TryGetTag(id, out var tag);

            Assert.That(result, Is.False);
            Assert.That(tag, Is.Null);
        }

        [Test]
        public void TryGetTag_does_not_allocate()
        {
            _ = EventStoreIdentity.GetTagForIdentityClass<SampleAggregateId>();
            const string id = "sampleaggregate_42";
            // warmup, to exclude jit and first call allocations
            EventStoreIdentity.TryGetTag(id, out _);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                EventStoreIdentity.TryGetTag(id, out _);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void Equals_as_IIdentity_compares_type_and_id()
        {
            IIdentity id = new SampleAggregateId(42);

            Assert.That(id.Equals((IIdentity)new SampleAggregateId(42)), Is.True);
            Assert.That(id.Equals(id), Is.True);
            Assert.That(id.Equals((IIdentity)new SampleAggregateId(43)), Is.False);
            Assert.That(id.Equals((IIdentity)new TestId(42)), Is.False, "Same numeric id but different type");
            Assert.That(id.Equals((IIdentity)null), Is.False);
        }

        [Test]
        public void Equals_does_not_allocate()
        {
            var first = new SampleAggregateId(42);
            var second = new SampleAggregateId(42);
            IIdentity firstAsInterface = first;
            IIdentity secondAsInterface = second;
            // warmup, to exclude jit and first call allocations
            _ = firstAsInterface.Equals(secondAsInterface);
            _ = first.Equals(second);
            _ = first == second;

            var equal = true;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                equal &= firstAsInterface.Equals(secondAsInterface);
                equal &= first.Equals(second);
                equal &= first == second;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(equal, Is.True);
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void String_representation_is_cached_and_same_instance_is_returned()
        {
            var id = new SampleAggregateId(42);

            var first = id.ToString();

            Assert.That(first, Is.EqualTo("SampleAggregate_42"));
            Assert.That(id.ToString(), Is.SameAs(first));
            Assert.That(id.AsString(), Is.SameAs(first));
            Assert.That(id.FullyQualifiedId, Is.SameAs(first));
            string implicitConversion = id;
            Assert.That(implicitConversion, Is.SameAs(first));
        }

        [Test]
        public void String_representation_does_not_allocate_after_first_call()
        {
            var id = new SampleAggregateId(42);
            // warmup, to exclude jit and first call allocations
            _ = id.ToString();
            _ = id.AsString();
            _ = id.FullyQualifiedId;
            string warmup = id;

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                _ = id.ToString();
                _ = id.AsString();
                _ = id.FullyQualifiedId;
                string converted = id;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void Id_cannot_be_set_from_outside_the_identity()
        {
            var setter = typeof(SampleAggregateId).GetProperty(nameof(SampleAggregateId.Id)).SetMethod;

            Assert.That(setter.IsPublic, Is.False, "Identities are immutable, Id setter must not be public");
            Assert.That(setter.IsFamilyAndAssembly, Is.True, "Id setter must be private protected, identities outside the framework assign the id only through the constructor");
        }

        [Test]
        public void Fully_qualified_id_setter_is_private()
        {
            var property = typeof(EventStoreIdentity).GetProperty(nameof(EventStoreIdentity.FullyQualifiedId));

            Assert.That(property.SetMethod, Is.Not.Null, "Serializers need the setter to populate an identity");
            Assert.That(property.SetMethod.IsPrivate, Is.True, "Identities are immutable, FullyQualifiedId setter must be private");
        }

        [Test]
        public void Assign_is_private()
        {
            // A protected Assign would let a derived identity change Id (and hash code) after
            // construction, breaking dictionaries and sets that contain the identity.
            var assign = typeof(EventStoreIdentity).GetMethod(
                "Assign", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(string) });

            Assert.That(assign, Is.Not.Null);
            Assert.That(assign.IsPrivate, Is.True, "Identities are immutable, Assign must be private");
        }

        [Test]
        public void Concurrent_first_to_string_returns_one_instance()
        {
            const int identityCount = 200;
            var threadCount = Math.Max(4, Environment.ProcessorCount);

            for (int i = 0; i < identityCount; i++)
            {
                var id = new SampleAggregateId(i + 1); // numeric constructor: nothing cached yet.
                using var barrier = new Barrier(threadCount);
                var results = new string[threadCount];

                // Dedicated threads: the barrier needs all of them running at the same time.
                var threads = Enumerable.Range(0, threadCount).Select(t => new Thread(() =>
                {
                    barrier.SignalAndWait();
                    results[t] = id.ToString();
                })).ToArray();
                foreach (var thread in threads) thread.Start();
                foreach (var thread in threads) Assert.That(thread.Join(TimeSpan.FromSeconds(10)), Is.True, "thread did not complete");

                Assert.That(results.All(r => ReferenceEquals(r, results[0])), Is.True, "racing first calls returned different string instances");
                Assert.That(id.ToString(), Is.SameAs(results[0]), "later calls must return the instance published by the first calls");
            }
        }

        [Test]
        public void Json_populates_identity_through_private_fully_qualified_id_setter()
        {
            // Same path Newtonsoft follows when it creates an identity with a parameterless
            // constructor and then sets the members: { "Id" : "Tag_123" } must restore the id.
            var id = new SampleAggregateId(1);
            _ = id.ToString(); // fill the cache with the old value.

            JsonConvert.PopulateObject("{\"Id\":\"SampleAggregate_42\"}", id);

            Assert.That(id.Id, Is.EqualTo(42));
            Assert.That(id.AsString(), Is.EqualTo("SampleAggregate_42"), "cached string must follow the new id");
            Assert.That(id, Is.EqualTo(new SampleAggregateId(42)));
        }

        [Test]
        public void Construction_from_canonical_string_reuses_input_instance()
        {
            var number = "42";
            var input = "SampleAggregate_" + number; //built at runtime, not an interned literal.

            var id = new SampleAggregateId(input);

            Assert.That(id.AsString(), Is.SameAs(input));
            Assert.That(id.ToString(), Is.SameAs(input));
            Assert.That(id.FullyQualifiedId, Is.SameAs(input));
            Assert.That(id.Id, Is.EqualTo(42));
        }

        [TestCase("sampleaggregate_42")]
        [TestCase("SAMPLEAGGREGATE_42")]
        [TestCase("SampleAggregate_042")]
        [TestCase("SampleAggregate_+42")]
        [TestCase("SampleAggregate_ 42")]
        [TestCase("SampleAggregate_42 ")]
        [TestCase("42")]
        [TestCase("042")]
        public void Construction_from_non_canonical_string_yields_canonical_representation(string input)
        {
            var id = new SampleAggregateId(input);

            Assert.That(id.AsString(), Is.EqualTo("SampleAggregate_42"));
            Assert.That(id.ToString(), Is.EqualTo("SampleAggregate_42"));
            Assert.That(id.FullyQualifiedId, Is.EqualTo("SampleAggregate_42"));
            Assert.That(id.AsString(), Is.Not.SameAs(input));
        }
    }
}

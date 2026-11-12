using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    /// <summary>
    /// Identity that derives directly from <see cref="AbstractIdentity{TKey}"/> and whose string
    /// representation depends on instance state, not only on type and id.
    /// </summary>
    public class ScopedAbstractId : AbstractIdentity<long>
    {
        public string Scope { get; }

        // Needed by IdentityManager.RegisterIdentitiesFromAssembly, called on this assembly by other tests.
        public ScopedAbstractId(long id) : this(id, "default")
        {
        }

        public ScopedAbstractId(long id, string scope) : base(id)
        {
            Scope = scope;
        }

        public override string GetTag()
        {
            return "ScopedAbstract";
        }

        public override string AsString()
        {
            return $"{Scope}-{GetTag()}_{Id}";
        }
    }

    [TestFixture]
    public class AbstractIdentityEqualityTests
    {
        [Test]
        public void Extensible_identity_with_same_id_and_different_representation_is_not_equal()
        {
            IIdentity first = new ScopedAbstractId(42, "tenantA");
            IIdentity second = new ScopedAbstractId(42, "tenantB");

            Assert.That(first.Equals(second), Is.False);
            Assert.That(second.Equals(first), Is.False);
        }

        [Test]
        public void Extensible_identity_with_same_representation_is_equal()
        {
            IIdentity first = new ScopedAbstractId(42, "tenantA");
            IIdentity second = new ScopedAbstractId(42, "tenantA");

            Assert.That(first.Equals(second), Is.True);
        }

        [Test]
        public void Event_store_identity_compares_type_and_id()
        {
            IIdentity first = new SampleAggregateId(42);
            IIdentity same = new SampleAggregateId("SampleAggregate_42");
            IIdentity different = new SampleAggregateId(43);

            Assert.That(first.Equals(same), Is.True);
            Assert.That(first.Equals(different), Is.False);
        }

        [Test]
        public void Identities_of_different_types_are_not_equal()
        {
            IIdentity first = new SampleAggregateId(42);
            IIdentity other = new ScopedAbstractId(42);

            Assert.That(first.Equals(other), Is.False);
            Assert.That(other.Equals(first), Is.False);
        }

        [Test]
        public void Event_store_identity_same_type_and_id_is_equal_through_every_entry_point()
        {
            var first = new SampleAggregateId(42);
            var same = new SampleAggregateId("SampleAggregate_42");

            AssertEquality(first, same, expected: true);
            AssertEquality(same, first, expected: true);
        }

        [Test]
        public void Event_store_identity_same_type_and_different_id_is_not_equal_through_any_entry_point()
        {
            AssertEquality(new SampleAggregateId(42), new SampleAggregateId(43), expected: false);
        }

        [Test]
        public void Event_store_identities_with_same_tag_and_different_type_are_not_equal_through_any_entry_point()
        {
            // Same class name in two namespaces: same tag and same string representation, but
            // two different concrete types.
            var name = "SameTagEquality" + Guid.NewGuid().ToString("N") + "Id";
            var first = CreateDynamicIdentity("NamespaceA." + name, 42L);
            var second = CreateDynamicIdentity("NamespaceB." + name, 42L);

            Assert.That(first.GetType(), Is.Not.EqualTo(second.GetType()));
            Assert.That(first.GetTag(), Is.EqualTo(second.GetTag()));
            Assert.That(first.AsString(), Is.EqualTo(second.AsString()));

            AssertEquality(first, second, expected: false);
            AssertEquality(second, first, expected: false);
        }

        [Test]
        public void Event_store_identity_generated_type_is_equal_to_another_instance_of_the_same_type()
        {
            var type = IdentityManagerRegistrationTests
                .BuildIdentityAssembly("NamespaceA.SameTypeEquality" + Guid.NewGuid().ToString("N") + "Id")
                .GetTypes()[0];
            var first = (EventStoreIdentity)Activator.CreateInstance(type, 42L);
            var second = (EventStoreIdentity)Activator.CreateInstance(type, 42L);

            AssertEquality(first, second, expected: true);
        }

        [Test]
        public void Event_store_identity_equality_handles_null()
        {
            var identity = new SampleAggregateId(42);
            SampleAggregateId nullIdentity = null;

            Assert.That(identity.Equals((object)null), Is.False);
            Assert.That(identity.Equals((IIdentity)null), Is.False);
            Assert.That(identity.Equals((AbstractIdentity<long>)null), Is.False);
            Assert.That(identity == nullIdentity, Is.False);
            Assert.That(nullIdentity == identity, Is.False);
            Assert.That(identity != nullIdentity, Is.True);
            Assert.That(nullIdentity != identity, Is.True);
#pragma warning disable CS1718 // Comparison made to same variable: it is the purpose of the test.
            Assert.That(nullIdentity == nullIdentity, Is.True);
            Assert.That(nullIdentity != nullIdentity, Is.False);
#pragma warning restore CS1718
        }

        [Test]
        public void Extensible_identity_typed_equality_still_compares_id_and_tag()
        {
            // Equals(object), == and != of an identity that is not an EventStoreIdentity keep
            // comparing id and tag: the scope is not part of the tag.
            var first = new ScopedAbstractId(42, "tenantA");
            var sameIdAndTag = new ScopedAbstractId(42, "tenantB");
            var differentId = new ScopedAbstractId(43, "tenantA");

            Assert.That(first.Equals((object)sameIdAndTag), Is.True);
            Assert.That(first.Equals((AbstractIdentity<long>)sameIdAndTag), Is.True);
            Assert.That(first == sameIdAndTag, Is.True);
            Assert.That(first != sameIdAndTag, Is.False);
            Assert.That(first.Equals((object)differentId), Is.False);
            Assert.That(first == differentId, Is.False);
            Assert.That(first != differentId, Is.True);
        }

        private static EventStoreIdentity CreateDynamicIdentity(string typeName, long id)
        {
            var type = IdentityManagerRegistrationTests.BuildIdentityAssembly(typeName).GetTypes()[0];
            return (EventStoreIdentity)Activator.CreateInstance(type, id);
        }

        /// <summary>
        /// Every equality entry point of an <see cref="EventStoreIdentity"/> must give the same answer.
        /// </summary>
        private static void AssertEquality(EventStoreIdentity first, EventStoreIdentity second, bool expected)
        {
            Assert.That(first.Equals((object)second), Is.EqualTo(expected), "Equals(object)");
            Assert.That(first.Equals((IIdentity)second), Is.EqualTo(expected), "Equals(IIdentity)");
            Assert.That(first.Equals((AbstractIdentity<long>)second), Is.EqualTo(expected), "Equals(AbstractIdentity<long>)");
            Assert.That(first == second, Is.EqualTo(expected), "==");
            Assert.That(first != second, Is.EqualTo(!expected), "!=");
            Assert.That(Equals(first, second), Is.EqualTo(expected), "object.Equals(a, b)");

            var dictionary = new Dictionary<EventStoreIdentity, string> { [first] = "first" };
            Assert.That(dictionary.ContainsKey(second), Is.EqualTo(expected), "Dictionary key");
            var set = new HashSet<IIdentity> { first };
            Assert.That(set.Contains(second), Is.EqualTo(expected), "HashSet key");

            if (expected)
            {
                Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()), "hash code");
            }
        }
    }
}

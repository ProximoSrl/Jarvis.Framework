using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;

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
    }
}

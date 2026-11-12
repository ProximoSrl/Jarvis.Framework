using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Tests.SharedTests.IdentitySupport;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Jarvis.Framework.Tests.SharedTests.Helpers
{
    [TestFixture]
    public class FastReflectionHelperTests
    {
        private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private class WithLong
        {
            public long Value { get; }

            public WithLong(long value)
            {
                Value = value;
            }
        }

        private class WithString
        {
            public string Value { get; }

            public WithString(string value)
            {
                Value = value;
            }

            public WithString(string value, int other)
            {
                Value = value + other;
            }
        }

        private class WithPrivateConstructor
        {
            public string Value { get; }

            private WithPrivateConstructor(string value)
            {
                Value = value;
            }
        }

        private class Throwing
        {
            public Throwing(string value)
            {
                throw new InvalidOperationException("ctor failed for " + value);
            }
        }

        [Test]
        public void Typed_activator_creates_instance_with_value_type_argument()
        {
            var ctor = typeof(WithLong).GetConstructor(new[] { typeof(long) });

            var activator = FastReflectionHelper.GetActivator<long, WithLong>(ctor);

            Assert.That(activator(42).Value, Is.EqualTo(42));
            Assert.That(activator(long.MaxValue).Value, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void Typed_activator_creates_instance_with_reference_type_argument()
        {
            var ctor = typeof(WithString).GetConstructor(new[] { typeof(string) });

            var activator = FastReflectionHelper.GetActivator<string, WithString>(ctor);

            Assert.That(activator("abc").Value, Is.EqualTo("abc"));
            Assert.That(activator(null).Value, Is.Null);
        }

        [Test]
        public void Typed_activator_creates_a_new_instance_for_each_call()
        {
            var ctor = typeof(WithString).GetConstructor(new[] { typeof(string) });
            var activator = FastReflectionHelper.GetActivator<string, WithString>(ctor);

            Assert.That(activator("abc"), Is.Not.SameAs(activator("abc")));
        }

        [Test]
        public void Typed_activator_can_return_a_base_type()
        {
            var ctor = typeof(WithString).GetConstructor(new[] { typeof(string) });

            Func<string, object> activator = FastReflectionHelper.GetActivator<string, object>(ctor);

            var instance = activator("abc");
            Assert.That(instance, Is.InstanceOf<WithString>());
            Assert.That(((WithString)instance).Value, Is.EqualTo("abc"));
        }

        [Test]
        public void Typed_activator_works_with_non_public_constructor()
        {
            var ctor = typeof(WithPrivateConstructor).GetConstructor(AllInstance, new[] { typeof(string) });

            var activator = FastReflectionHelper.GetActivator<string, WithPrivateConstructor>(ctor);

            Assert.That(activator("abc").Value, Is.EqualTo("abc"));
        }

        [Test]
        public void Typed_activator_rejects_constructor_with_different_argument_type()
        {
            var ctor = typeof(WithLong).GetConstructor(new[] { typeof(long) });

            var ex = Assert.Throws<ArgumentException>(() => FastReflectionHelper.GetActivator<string, WithLong>(ctor));

            Assert.That(ex.Message, Does.Contain(typeof(WithLong).FullName));
        }

        [Test]
        public void Typed_activator_rejects_constructor_with_more_than_one_argument()
        {
            var ctor = typeof(WithString).GetConstructor(new[] { typeof(string), typeof(int) });

            Assert.Throws<ArgumentException>(() => FastReflectionHelper.GetActivator<string, WithString>(ctor));
        }

        [Test]
        public void Typed_activator_does_not_wrap_constructor_exception()
        {
            var ctor = typeof(Throwing).GetConstructor(new[] { typeof(string) });
            var activator = FastReflectionHelper.GetActivator<string, Throwing>(ctor);

            var ex = Assert.Throws<InvalidOperationException>(() => activator("abc"));

            Assert.That(ex.Message, Is.EqualTo("ctor failed for abc"));
        }

        [Test]
        public void Typed_activator_allocates_only_the_instance()
        {
            var ctor = typeof(WithLong).GetConstructor(new[] { typeof(long) });
            var activator = FastReflectionHelper.GetActivator<long, WithLong>(ctor);
            WithLong created = null;
            var baseline = IdentityAllocationTests.Measure(() => created = new WithLong(42));

            var allocated = IdentityAllocationTests.Measure(() => created = activator(42));

            Assert.That(created.Value, Is.EqualTo(42));
            Assert.That(baseline, Is.GreaterThan(0));
            Assert.That(allocated, Is.EqualTo(baseline), "No object[] nor boxed argument must be allocated");
        }

        [Test]
        public void Untyped_activator_allocates_more_than_the_typed_one()
        {
            // Documents why the typed activator exists and proves that the measure detects the
            // object[] and the boxed argument.
            var ctor = typeof(WithLong).GetConstructor(new[] { typeof(long) });
            var typed = FastReflectionHelper.GetActivator<long, WithLong>(ctor);
            var untyped = FastReflectionHelper.GetActivator(ctor);
            object created = null;

            var typedAllocated = IdentityAllocationTests.Measure(() => created = typed(42));
            var untypedAllocated = IdentityAllocationTests.Measure(() => created = untyped(42L));

            Assert.That(created, Is.Not.Null);
            Assert.That(untypedAllocated, Is.GreaterThan(typedAllocated));
        }

        [Test]
        public void Untyped_activator_still_creates_instance()
        {
            var ctor = typeof(WithLong).GetConstructor(new[] { typeof(long) });

            var activator = FastReflectionHelper.GetActivator(ctor);

            Assert.That(((WithLong)activator(42L)).Value, Is.EqualTo(42));
        }
    }
}

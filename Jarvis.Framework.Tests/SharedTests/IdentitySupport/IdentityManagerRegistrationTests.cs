using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.IdentitySupport;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    /// <summary>
    /// Registration checks that do not need a database. Identities are generated in a dynamic
    /// assembly, so each test registers exactly the identity it needs and nothing else.
    /// </summary>
    [TestFixture]
    public class IdentityManagerRegistrationTests
    {
        [Test]
        public void Identity_with_parameterless_constructor_is_registered()
        {
            // Allowed: serializers that use the parameterless constructor restore the id
            // through the private FullyQualifiedId setter.
            var assembly = BuildIdentityAssembly("ParameterlessCtorId", withParameterlessConstructor: true);
            var sut = new IdentityManager(null);

            sut.RegisterIdentitiesFromAssembly(assembly);

            var identity = sut.ToIdentity("ParameterlessCtor_42");
            Assert.That(identity.AsString(), Is.EqualTo("ParameterlessCtor_42"));
        }

        [Test]
        public void Identity_without_parameterless_constructor_is_registered()
        {
            var assembly = BuildIdentityAssembly("OnlyLongCtorId", withParameterlessConstructor: false);
            var sut = new IdentityManager(null);

            sut.RegisterIdentitiesFromAssembly(assembly);

            var identity = sut.ToIdentity("OnlyLongCtor_42");
            Assert.That(identity.AsString(), Is.EqualTo("OnlyLongCtor_42"));
        }

        [Test]
        public void Identity_whose_constructor_changes_the_id_does_not_reuse_input_string()
        {
            // The (long id) constructor calls base(id + 1): the parsed string no longer describes
            // the identity, so it must not become its cached string representation.
            var assembly = BuildIdentityAssembly("NormalizingCtorId", withParameterlessConstructor: false, idOffset: 1);
            var sut = new IdentityManager(null);
            sut.RegisterIdentitiesFromAssembly(assembly);

            var identity = (EventStoreIdentity)sut.ToIdentity("NormalizingCtor_42");

            Assert.That(identity.Id, Is.EqualTo(43));
            Assert.That(identity.AsString(), Is.EqualTo("NormalizingCtor_43"));
            Assert.That(identity.ToString(), Is.EqualTo("NormalizingCtor_43"));
        }

        [Test]
        public void Identities_whose_tags_differ_only_in_casing_are_rejected()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var firstName = "CaseClash" + suffix + "Id";
            var secondName = "CASECLASH" + suffix.ToUpperInvariant() + "Id";
            var sut = new IdentityManager(null);
            sut.RegisterIdentitiesFromAssembly(BuildIdentityAssembly(firstName));

            var ex = Assert.Throws<JarvisFrameworkEngineException>(
                () => sut.RegisterIdentitiesFromAssembly(BuildIdentityAssembly(secondName)));

            Assert.That(ex.Message, Does.Contain(secondName));
            Assert.That(ex.Message, Does.Contain("differs only in casing"));
            // the first registration is untouched.
            var identity = sut.ToIdentity("caseclash" + suffix + "_1");
            Assert.That(identity.GetType().Name, Is.EqualTo(firstName));
        }

        [Test]
        public void Registering_the_same_assembly_twice_is_allowed()
        {
            var assembly = BuildIdentityAssembly("RegisteredTwice" + Guid.NewGuid().ToString("N") + "Id");
            var sut = new IdentityManager(null);
            sut.RegisterIdentitiesFromAssembly(assembly);

            Assert.DoesNotThrow(() => sut.RegisterIdentitiesFromAssembly(assembly));
        }

        [Test]
        public void Seeding_does_not_replace_string_already_cached_by_the_constructor()
        {
            // The (long id) constructor calls ToString(): the string it produced may already have
            // been handed out (e.g. logged), seeding with the parsed input must not replace it.
            var assembly = BuildIdentityAssembly("ToStringInCtorId", callToStringInConstructor: true);
            var sut = new IdentityManager(null);
            sut.RegisterIdentitiesFromAssembly(assembly);
            var number = "42";
            var input = "ToStringInCtor_" + number; //built at runtime, not an interned literal.

            var identity = (EventStoreIdentity)sut.ToIdentity(input);

            var first = identity.ToString();
            Assert.That(first, Is.EqualTo(input));
            Assert.That(first, Is.Not.SameAs(input), "the string cached by the constructor was replaced by the parsed input");
            Assert.That(identity.ToString(), Is.SameAs(first));
        }

        /// <summary>
        /// Emits <c>public class {name} : EventStoreIdentity</c> with a public (long id) constructor
        /// that calls base(id + idOffset), then optionally ToString(), and, optionally, a private
        /// parameterless constructor that calls base(1).
        /// </summary>
        internal static Assembly BuildIdentityAssembly(
            string typeName,
            bool withParameterlessConstructor = false,
            long idOffset = 0,
            bool callToStringInConstructor = false)
        {
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("DynamicIdentities_" + Guid.NewGuid().ToString("N")),
                AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("Main");
            var typeBuilder = moduleBuilder.DefineType(
                typeName,
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(EventStoreIdentity));

            var baseLongConstructor = typeof(EventStoreIdentity).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                new[] { typeof(long) });

            var longConstructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(long) });
            var il = longConstructor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            if (idOffset != 0)
            {
                il.Emit(OpCodes.Ldc_I8, idOffset);
                il.Emit(OpCodes.Add);
            }
            il.Emit(OpCodes.Call, baseLongConstructor);
            if (callToStringInConstructor)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Callvirt, typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes));
                il.Emit(OpCodes.Pop);
            }
            il.Emit(OpCodes.Ret);

            if (withParameterlessConstructor)
            {
                var parameterless = typeBuilder.DefineConstructor(
                    MethodAttributes.Private, CallingConventions.Standard, Type.EmptyTypes);
                il = parameterless.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I8, 1L);
                il.Emit(OpCodes.Call, baseLongConstructor);
                il.Emit(OpCodes.Ret);
            }

            typeBuilder.CreateType();
            return assemblyBuilder;
        }
    }
}

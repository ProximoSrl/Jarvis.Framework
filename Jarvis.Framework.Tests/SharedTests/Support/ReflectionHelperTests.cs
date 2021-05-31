using Jarvis.Framework.Shared.Support;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Tests.SharedTests.Support
{
    [TestFixture]
    [Category("Serialization")]
    public class ReflectionHelperTests
    {
        [TestCase(typeof(string), "System.String")]
        [TestCase(typeof(string[]), "System.String[]")]
        [TestCase(typeof(List<String>), "System.Collections.Generic.List`1[[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]")]
        [TestCase(typeof(Dictionary<String, string>), "System.Collections.Generic.Dictionary`2[[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]")]
        public void Basic_conversion(Type type, String expected)
        {
            Assert.That(type.GetFullNameWithTypeForwarded(), Is.EqualTo(expected));

#if NET461_OR_GREATER
            Assert.That(expected, Is.EqualTo(type.FullName));
#endif
        }

        [TestCase(typeof(List<List<String>>), "System.Collections.Generic.List`1[[System.Collections.Generic.List`1[[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]")]
        [TestCase(typeof(Dictionary<string, string[]>), "System.Collections.Generic.Dictionary`2[[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]")]
        [TestCase(typeof(Dictionary<string, Dictionary<string, string[]>>), "System.Collections.Generic.Dictionary`2[[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.Collections.Generic.Dictionary`2[[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]")]
        public void Nested_conversion(Type type, String expected)
        {
            Console.WriteLine(type.FullName);

#if NET461_OR_GREATER
            Assert.That(expected, Is.EqualTo(type.FullName));
#endif
            Assert.That(type.GetFullNameWithTypeForwarded(), Is.EqualTo(expected));
        }

        [TestCase(typeof(ClassWithNested), "Jarvis.Framework.Tests.SharedTests.Support.ReflectionHelperTests+ClassWithNested")]
        [TestCase(typeof(List<ClassWithNested>), "System.Collections.Generic.List`1[[Jarvis.Framework.Tests.SharedTests.Support.ReflectionHelperTests+ClassWithNested, Jarvis.Framework.Tests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]")]
        [TestCase(typeof(List<ClassWithNested.Nested>), "System.Collections.Generic.List`1[[Jarvis.Framework.Tests.SharedTests.Support.ReflectionHelperTests+ClassWithNested+Nested, Jarvis.Framework.Tests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]")]

        public void Test_nested_class(Type type, String expected)
        {
            Console.WriteLine(type.FullName);
#if NET461_OR_GREATER
            Assert.That(expected, Is.EqualTo(type.FullName));
#endif
            Assert.That(type.GetFullNameWithTypeForwarded(), Is.EqualTo(expected));
        }

        public class ClassWithNested
        {
            public Nested NestedProperty { get; set; }

            public class Nested
            {
                public Int32 Number { get; set; }
            }
        }
    }
}

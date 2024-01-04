using Jarvis.Framework.Shared.Domain;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.EngineTests
{
    [TestFixture]
    public class StringIdTests
    {
        public class SampleId : StringValue
        {
            public SampleId(string value)
                : base(value)
            {
            }
        }

        public class UppercaseSampleId : UppercaseStringValue
        {
            public UppercaseSampleId(string value)
                : base(value)
            {
            }
        }

        public class AnotherSampleId : StringValue
        {
            public AnotherSampleId(string value) : base(value)
            {
            }
        }

        [Test]
        public void id_should_be_uppercase()
        {
            var id = new UppercaseSampleId("abc");
            NUnit.Framework.Legacy.ClassicAssert.AreEqual("ABC", (string)id);
        }

        [Test]
        public void id_setter_should_convert_to_uppercase()
        {
            var id = new UppercaseSampleId("abc");
            NUnit.Framework.Legacy.ClassicAssert.AreEqual("ABC", (string)id);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        public void null_or_whitespace_id_should_be_invalid(string val)
        {
            var id = new SampleId(val);
            NUnit.Framework.Legacy.ClassicAssert.IsFalse(id.IsValid());
        }

        [Test]
        public void test_equality()
        {
            var a = new SampleId("a");
            var b = new SampleId("a");

            NUnit.Framework.Legacy.ClassicAssert.AreEqual(a, b);
            NUnit.Framework.Legacy.ClassicAssert.IsTrue(a.Equals(b));

            NUnit.Framework.Legacy.ClassicAssert.IsTrue(a.Equals(a));
            NUnit.Framework.Legacy.ClassicAssert.IsTrue(a == b);
        }

        [Test]
        public void test_inequality()
        {
            var a = new SampleId("a");
            var b = new SampleId("b");

            NUnit.Framework.Legacy.ClassicAssert.AreNotEqual(a, null);
            NUnit.Framework.Legacy.ClassicAssert.AreNotEqual(a, b);

            NUnit.Framework.Legacy.ClassicAssert.IsFalse(a.Equals(b));
            NUnit.Framework.Legacy.ClassicAssert.IsFalse(a.Equals(null));

            NUnit.Framework.Legacy.ClassicAssert.IsFalse(a == b);
        }

        [Test]
        public void different_classes_with_same_id_value_should_not_be_equal()
        {
            var a = new SampleId("a");
            var b = new AnotherSampleId("a");

            NUnit.Framework.Legacy.ClassicAssert.IsFalse(a.Equals(b));
        }
    }
}

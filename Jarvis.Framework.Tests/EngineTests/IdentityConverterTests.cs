using Jarvis.Framework.Shared.IdentitySupport;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Jarvis.Framework.Tests.EngineTests
{
    [TestFixture]
    public class IdentityConverterTests
    {
        private IdentityManager _manager;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            _manager = new IdentityManager(new InMemoryCounterService());
            _manager.RegisterIdentitiesFromAssembly(GetType().Assembly);
        }

        [Test]
        public void convert_to_string()
        {
            var id = new SampleAggregateId(1);
            var asString = _manager.ToString(id);

            NUnit.Framework.Legacy.ClassicAssert.AreEqual("SampleAggregate_1", asString);
        }

        [Test]
        public void convert_to_identity()
        {
            var asString = EventStoreIdentity.Format(typeof(SampleAggregateId), 1L);
            var identity = (EventStoreIdentity)_manager.ToIdentity(asString);

            NUnit.Framework.Legacy.ClassicAssert.IsTrue(identity is SampleAggregateId);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual("SampleAggregate", identity.GetTag());
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1L, identity.Id);
        }

        //[Test, Explicit]
        public void Convert_lots_of_identities()
        {
            Stopwatch timer = new Stopwatch();
            List<String> test = new List<String>();
            _manager.ToIdentity("SampleAggregate_2");
            for (int i = 0; i < 1000000; i++)
            {
                test.Add(new SampleAggregateId(i).ToString());
            }
            timer.Start();
            foreach (var item in test)
            {
                _manager.ToIdentity(item);
            }
            timer.Stop();
            Console.WriteLine("Elapsed: {0}", timer.ElapsedMilliseconds);
        }

        [Test]
        public void as_string_and_to_string_are_equal()
        {
            var id = new SampleAggregateId(555);

            var asString = id.AsString();
            var toString = id.ToString();

            NUnit.Framework.Legacy.ClassicAssert.AreEqual(asString, toString);
        }

        [Test]
        public void null_string_should_be_transalted_to_null_identity()
        {
            var identity = _manager.ToIdentity(null);

            NUnit.Framework.Legacy.ClassicAssert.IsNull(identity);
        }

        [Test]
        public void empty_string_should_be_transalted_to_null_identity()
        {
            var identity = _manager.ToIdentity(String.Empty);

            NUnit.Framework.Legacy.ClassicAssert.IsNull(identity);
        }

        [Test]
        public void whitespace_identity_should_throw_exception()
        {
            var ex = Assert.Catch<Exception>(() => _manager.ToIdentity(" "));

            NUnit.Framework.Legacy.ClassicAssert.AreEqual("invalid identity value  ", ex.Message);
        }

        [Test]
        public void unknown_identity_should_throw_exception()
        {
            var ex = Assert.Catch<Exception>(() => _manager.ToIdentity("pluto_1"));

            NUnit.Framework.Legacy.ClassicAssert.AreEqual("pluto not registered in IdentityManager", ex.Message);
        }

        [TestCase("SampleAggregate_1")]
        [TestCase("sampleaggregate_1")]
        [TestCase("samPLeaggrEGate_1")]
        [TestCase("SAMPLEAGGREGATE_1")]
        public void Verify_to_identity_is_case_insensitive(String aggregateIdString)
        {
            var identity = _manager.ToIdentity(aggregateIdString);

            Assert.That(identity.AsString(), Is.EqualTo(new SampleAggregateId(1).AsString()));
        }

        [TestCase("SampleAggregate_-1")]
        [TestCase("SampleAggregate_1_2")]
        [TestCase("_1")]
        [TestCase("1")]
        [TestCase("ASAMPLEAGGREGATE_1")]
        [TestCase("SAMPLEAGGREGATE__1")]
        public void To_identity_rejects_invalid_formats(String aggregateIdString)
        {
            Assert.Throws<JarvisFrameworkIdentityException>(() => _manager.ToIdentity(aggregateIdString));
        }

        [TestCase("SampleAggregate_20|458ca8a2-d165-45ba-8403-4c74c01e7fe1")]
        [TestCase("SampleAggregate_20|1")]
        [TestCase("SampleAggregate_20_1")]
        public void Verify_not_parse_extra_char(string id)
        {
            Assert.Throws<JarvisFrameworkIdentityException>(() => new SampleAggregateId(id));
        }
    }
}

using Castle.Core.Logging;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    [TestFixture]
    public class AbstractIdentityTranslatorTests
    {
        protected IMongoDatabase _db;
        private IdentityManager _identityManager;
        protected ILogger testLogger = NullLogger.Instance;
        private Int64 _seed = 1;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            TestHelper.RegisterSerializerForFlatId<TestId>();
            TestHelper.RegisterSerializerForFlatId<TestFlatId>();
            _db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            _db.Drop();
            _identityManager = new IdentityManager(new CounterService(_db));
            _identityManager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
        }

        [Test]
        public void Verify_basic_translation()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var id = sut.MapWithAutomaticCreate(key);
            var secondCall = sut.MapWithAutomaticCreate(key);
            Assert.That(id, Is.EqualTo(secondCall));
        }

        [Test]
        public void Verify_identity_Translation()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            MyAggregateId id = new MyAggregateId(_seed++);
            var mappedIdentity = sut.MapWithAutomaticCreate(id.AsString());

            var reverseMap = sut.ReverseMap(mappedIdentity);
            Assert.That(reverseMap, Is.EqualTo(id.AsString().ToLowerInvariant()));
        }

        [Test]
        public void Verify_reverse_translation()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var id = sut.MapWithAutomaticCreate(key);
            var reversed = sut.ReverseMap(id);
            Assert.That(reversed, Is.EqualTo(key));
        }

        [Test]
        public void Verify_reverse_translation_is_not_case_sensitive()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString() + "CASE_UPPER";
            var id = sut.MapWithAutomaticCreate(key);
            var reversed = sut.ReverseMap(id);
            Assert.That(reversed, Is.EqualTo(key.ToLowerInvariant()));
        }

        [Test]
        public void Verify_translation_multiple()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();
            String key3 = Guid.NewGuid().ToString();

            var id1 = sut.MapWithAutomaticCreate(key1);
            var id2 = sut.MapWithAutomaticCreate(key2);

            var multimap = sut.GetMultipleMapWithoutAutoCreation(key1, key2);
            Assert.That(multimap[key1], Is.EqualTo(id1));
            Assert.That(multimap[key2], Is.EqualTo(id2));
            Assert.That(multimap.ContainsKey(key3), Is.False);
        }

        [Test]
        public void Verify_translation_multiple_case_insensitive()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString() + "UPPER";
            String key2 = Guid.NewGuid().ToString() + "UPPER";
            String key3 = Guid.NewGuid().ToString() + "UPPER";

            var id1 = sut.MapWithAutomaticCreate(key1);
            var id2 = sut.MapWithAutomaticCreate(key2);

            var multimap = sut.GetMultipleMapWithoutAutoCreation(key1, key2);
            Assert.That(multimap[key1], Is.EqualTo(id1));
            Assert.That(multimap[key2], Is.EqualTo(id2));
            Assert.That(multimap.ContainsKey(key3), Is.False);
        }

        [Test]
        public void Verify_translation_multiple_resilient_to_null()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);

            var multimap = sut.GetMultipleMapWithoutAutoCreation(new string[] { });
            Assert.That(multimap.Count, Is.EqualTo(0));

            multimap = sut.GetMultipleMapWithoutAutoCreation((string[])null);
            Assert.That(multimap.Count, Is.EqualTo(0));
        }

        [Test]
        public void Verify_reverse_translation_multiple()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();
            String key3 = Guid.NewGuid().ToString();

            var id1 = sut.MapWithAutomaticCreate(key1);
            var id2 = sut.MapWithAutomaticCreate(key2);
            var id3 = sut.MapWithAutomaticCreate(key3);

            var reversed = sut.ReverseMap(id1, id2, id3);
            Assert.That(reversed[id1], Is.EqualTo(key1));
            Assert.That(reversed[id2], Is.EqualTo(key2));
            Assert.That(reversed[id3], Is.EqualTo(key3));
        }

        [Test]
        public void Verify_reverse_translation_with_multiple_aliases_returns_first_mapped()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            string alias = Guid.NewGuid().ToString();

            var id1 = sut.MapWithAutomaticCreate(key1);
            sut.AddAlias(id1, alias);

            var reversed = sut.ReverseMap(id1);
            Assert.That(reversed, Is.EqualTo(key1));
        }

        [Test]
        public void Verify_reverse_translation_with_multiple_aliases_returns_dictionary()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            string alias = Guid.NewGuid().ToString();

            var id1 = sut.MapWithAutomaticCreate(key1);
            sut.AddAlias(id1, alias);

            var reversed = sut.ReverseMap(new[] { id1 });
            Assert.That(reversed[id1], Is.EqualTo(key1));
        }

        [Test]
        public void Verify_reverse_translation_with_multiple_aliases()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            string alias = Guid.NewGuid().ToString();

            var id1 = sut.MapWithAutomaticCreate(key1);
            sut.AddAlias(id1, alias);

            var reversed = sut.ReverseMaps(id1);
            Assert.That(reversed.Length, Is.EqualTo(2));
            Assert.That(reversed[0], Is.EqualTo(key1));
            Assert.That(reversed[1], Is.EqualTo(alias));
        }

        [Test]
        public void Verify_reverse_translation_is_resilient_to_missing_id()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();

            var id1 = sut.MapWithAutomaticCreate(key1);
            var id2 = sut.MapWithAutomaticCreate(key2);
            var id3 = new SampleAggregateId(100000);

            var reversed = sut.ReverseMap(id1, id2, id3);
            Assert.That(reversed[id1], Is.EqualTo(key1));
            Assert.That(reversed[id2], Is.EqualTo(key2));
            Assert.That(reversed.ContainsKey(id3), Is.False);
        }

        [Test]
        public void Verify_reverse_translation_is_resilient_to_empty_list()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var reversed = sut.ReverseMap(new SampleAggregateId[] { });
            Assert.That(reversed.Count, Is.EqualTo(0));

            reversed = sut.ReverseMap((SampleAggregateId[])null);
            Assert.That(reversed.Count, Is.EqualTo(0));
        }

        [Test]
        public void Verify_alias_to_id()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();
            var identity = sut.MapWithAutomaticCreate(key1);
            sut.AddAlias(identity, key2);

            //Both keys gets mapped to the very same identity.
            Assert.That(sut.MapWithWithoutAutomaticCreate(key1), Is.EqualTo(identity));
            Assert.That(sut.MapWithWithoutAutomaticCreate(key2), Is.EqualTo(identity));

            Assert.That(sut.MapWithAutomaticCreate(key1), Is.EqualTo(identity));
            Assert.That(sut.MapWithAutomaticCreate(key2), Is.EqualTo(identity));
        }

        [Test]
        public void Verify_alias_reverse_mapping()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();
            var identity = sut.MapWithAutomaticCreate(key1);
            sut.AddAlias(identity, key2);

            var key = sut.ReverseMap(identity);
            Assert.That(key, Is.EqualTo(key1));
        }

        private class TestMapper : AbstractIdentityTranslator<SampleAggregateId>
        {
            public TestMapper(IMongoDatabase systemDB, IIdentityGenerator identityGenerator) : base(systemDB, identityGenerator)
            {
            }

            public SampleAggregateId MapWithAutomaticCreate(String key)
            {
                return Translate(key, true);
            }

            public SampleAggregateId MapWithWithoutAutomaticCreate(String key)
            {
                return Translate(key, false);
            }

            public IDictionary<String, SampleAggregateId> GetMultipleMapWithoutAutoCreation(params String[] keys)
            {
                return base.GetMultipleMapWithoutAutoCreation(keys);
            }

            public string ReverseMap(SampleAggregateId id)
            {
                return GetAlias(id);
            }

            public IDictionary<SampleAggregateId, String> ReverseMap(params SampleAggregateId[] ids)
            {
                return GetAliases(ids);
            }

            public string[] ReverseMaps(SampleAggregateId id)
            {
                return GetAliases(id);
            }

            public new void AddAlias(SampleAggregateId key, string alias)
            {
                base.AddAlias(key, alias);
            }
        }
    }
}

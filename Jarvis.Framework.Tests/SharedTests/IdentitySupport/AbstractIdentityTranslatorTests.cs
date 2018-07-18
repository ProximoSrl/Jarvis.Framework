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
using System.Linq;
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
            var id = sut.MapWithAutCreate(key);
            var secondCall = sut.MapWithAutCreate(key);
            Assert.That(id, Is.EqualTo(secondCall));
        }

        [Test]
        public void Verify_identity_Translation()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            MyAggregateId id = new MyAggregateId(_seed++);
            var mappedIdentity = sut.MapIdentity(id);

            var reverseMap = sut.ReverseMap(mappedIdentity);
            Assert.That(reverseMap, Is.EqualTo(id.AsString().ToLowerInvariant()));
        }

        [Test]
        public void Verify_reverse_translation()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var id = sut.MapWithAutCreate(key);
            var reversed = sut.ReverseMap(id);
            Assert.That(reversed, Is.EqualTo(key));
        }

        [Test]
        public void Verify_reverse_translation_is_not_case_sensitive()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString() + "CASE_UPPER";
            var id = sut.MapWithAutCreate(key);
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

            var id1 = sut.MapWithAutCreate(key1);
            var id2 = sut.MapWithAutCreate(key2);

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

            var id1 = sut.MapWithAutCreate(key1);
            var id2 = sut.MapWithAutCreate(key2);

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

            var id1 = sut.MapWithAutCreate(key1);
            var id2 = sut.MapWithAutCreate(key2);
            var id3 = sut.MapWithAutCreate(key3);

            var reversed = sut.ReverseMap(id1,id2, id3);
            Assert.That(reversed[id1], Is.EqualTo(key1));
            Assert.That(reversed[id2], Is.EqualTo(key2));
            Assert.That(reversed[id3], Is.EqualTo(key3));
        }

        [Test]
        public void Verify_reverse_translation_is_resilient_to_missing_id()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();

            var id1 = sut.MapWithAutCreate(key1);
            var id2 = sut.MapWithAutCreate(key2);
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

            reversed = sut.ReverseMap(( SampleAggregateId[]) null);
            Assert.That(reversed.Count, Is.EqualTo(0));
        }

        private class TestMapper : AbstractIdentityTranslator<SampleAggregateId>
        {
            public TestMapper(IMongoDatabase systemDB, IIdentityGenerator identityGenerator) : base(systemDB, identityGenerator)
            {
            }

            public SampleAggregateId MapWithAutCreate(String key)
            {
                return base.Translate(key, true);
            }

            public SampleAggregateId MapIdentity(IIdentity identity)
            {
                return Translate(identity.AsString(), true);
            }

            public IDictionary<String, SampleAggregateId> GetMultipleMapWithoutAutoCreation(params String[] keys)
            {
                return base.GetMultipleMapWithoutAutoCreation(keys);
            }

            public string ReverseMap(SampleAggregateId id)
            {
                return base.GetAlias(id);
            }

            public IDictionary<SampleAggregateId, String> ReverseMap(params SampleAggregateId[] ids)
            {
                return base.GetAliases(ids);
            }
        }
    }
}

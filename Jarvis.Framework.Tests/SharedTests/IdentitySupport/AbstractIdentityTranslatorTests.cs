using Castle.Core.Logging;
using Jarvis.Framework.Shared.Exceptions;
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
            Assert.That(id.AsString(), Is.EqualTo(secondCall.AsString()));
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
            Assert.That(multimap[key1].AsString(), Is.EqualTo(id1.AsString()));
            Assert.That(multimap[key2].AsString(), Is.EqualTo(id2.AsString()));
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
            Assert.That(multimap[key1].AsString(), Is.EqualTo(id1.AsString()));
            Assert.That(multimap[key2].AsString(), Is.EqualTo(id2.AsString()));
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
            Assert.That(sut.MapWithWithoutAutomaticCreate(key1) == identity);
            Assert.That(sut.MapWithWithoutAutomaticCreate(key2) == identity);

            Assert.That(sut.MapWithAutomaticCreate(key1) == identity);
            Assert.That(sut.MapWithAutomaticCreate(key2) == identity);
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

        [Test]
        public void Verify_try_translate_existing_key()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var id = sut.MapWithAutomaticCreate(key);
            var tryResult = sut.TryTranslateKey(key);
            Assert.That(tryResult.AsString(), Is.EqualTo(id.AsString()));
        }

        [Test]
        public void Verify_try_translate_missing_key_returns_null()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var tryResult = sut.TryTranslateKey(key);
            Assert.That(tryResult, Is.Null);
        }

        [Test]
        public void Verify_try_translate_null_key_throws()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            Assert.Throws<ArgumentNullException>(() => sut.TryTranslateKey(null));
        }

        [Test]
        public void Verify_replace_alias()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();
            String newAlias = Guid.NewGuid().ToString();

            var identity = sut.MapWithAutomaticCreate(key1);
            sut.AddAlias(identity, key2);

            // Verify both aliases work
            Assert.That(sut.MapWithWithoutAutomaticCreate(key1).AsString(), Is.EqualTo(identity.AsString()));
            Assert.That(sut.MapWithWithoutAutomaticCreate(key2).AsString(), Is.EqualTo(identity.AsString()));

            // Replace aliases
            sut.ReplaceAliasForKey(identity, newAlias);

            // Old aliases should not work, new one should
            Assert.Throws<JarvisFrameworkEngineException>(() => sut.MapWithWithoutAutomaticCreate(key1));
            Assert.Throws<JarvisFrameworkEngineException>(() => sut.MapWithWithoutAutomaticCreate(key2));
            Assert.That(sut.MapWithWithoutAutomaticCreate(newAlias).AsString(), Is.EqualTo(identity.AsString()));
        }

        [Test]
        public void Verify_replace_alias_null_throws()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var identity = new SampleAggregateId(1);
            Assert.Throws<ArgumentNullException>(() => sut.ReplaceAliasForKey(identity, null));
        }

        [Test]
        public void Verify_delete_aliases()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();

            var identity = sut.MapWithAutomaticCreate(key1);
            sut.AddAlias(identity, key2);

            // Verify both aliases work
            Assert.That(sut.MapWithWithoutAutomaticCreate(key1).AsString(), Is.EqualTo(identity.AsString()));
            Assert.That(sut.MapWithWithoutAutomaticCreate(key2).AsString(), Is.EqualTo(identity.AsString()));

            // Delete all aliases
            sut.DeleteAliasesForKey(identity);

            // Both should now fail
            Assert.Throws<JarvisFrameworkEngineException>(() => sut.MapWithWithoutAutomaticCreate(key1));
            Assert.Throws<JarvisFrameworkEngineException>(() => sut.MapWithWithoutAutomaticCreate(key2));
        }

        [Test]
        public void Verify_translate_null_key_throws()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            Assert.Throws<ArgumentNullException>(() => sut.MapWithAutomaticCreate(null));
        }

        [Test]
        public void Verify_translate_missing_key_without_auto_create_throws()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            Assert.Throws<JarvisFrameworkEngineException>(() => sut.MapWithWithoutAutomaticCreate(key));
        }

        [Test]
        public void Verify_add_alias_null_throws()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var identity = new SampleAggregateId(1);
            Assert.Throws<ArgumentNullException>(() => sut.AddAlias(identity, null));
        }

        [Test]
        public void Verify_add_alias_duplicate_with_same_key_succeeds()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            String alias = Guid.NewGuid().ToString();

            var identity = sut.MapWithAutomaticCreate(key);
            sut.AddAlias(identity, alias);

            // Adding the same alias to the same identity should not throw
            Assert.DoesNotThrow(() => sut.AddAlias(identity, alias));
        }

        [Test]
        public void Verify_add_alias_duplicate_with_different_key_throws()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key1 = Guid.NewGuid().ToString();
            String key2 = Guid.NewGuid().ToString();
            String alias = Guid.NewGuid().ToString();

            var identity1 = sut.MapWithAutomaticCreate(key1);
            var identity2 = sut.MapWithAutomaticCreate(key2);

            sut.AddAlias(identity1, alias);

            // Adding the same alias to a different identity should throw
            Assert.Throws<JarvisFrameworkEngineException>(() => sut.AddAlias(identity2, alias));
        }

        [Test]
        public void Verify_get_alias_null_key_throws()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            Assert.Throws<ArgumentNullException>(() => sut.ReverseMap((SampleAggregateId)null));
        }

        [Test]
        public void Verify_get_aliases_null_key_throws()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            Assert.Throws<ArgumentNullException>(() => sut.ReverseMaps(null));
        }

        [Test]
        public void Verify_get_alias_returns_null_for_unmapped_id()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var unmappedId = new SampleAggregateId(999999);
            var result = sut.ReverseMap(unmappedId);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Verify_get_aliases_returns_empty_for_unmapped_id()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var unmappedId = new SampleAggregateId(999999);
            var result = sut.ReverseMaps(unmappedId);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(0));
        }

        [Test]
        public void Verify_map_identity_creates_new_mapping()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var identity = _identityManager.New<SampleAggregateId>();

            var mappedIdentity = sut.MapIdentityPublic(key, identity);

            Assert.That(mappedIdentity, Is.Not.Null);
            Assert.That(mappedIdentity.ExternalKey, Is.EqualTo(key.ToLowerInvariant()));
            Assert.That(mappedIdentity.AggregateId.AsString(), Is.EqualTo(identity.AsString()));
        }

        [Test]
        public void Verify_map_identity_returns_existing_on_duplicate()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var identity1 = _identityManager.New<SampleAggregateId>();
            var identity2 = _identityManager.New<SampleAggregateId>();

            // Create first mapping
            var firstMapping = sut.MapIdentityPublic(key, identity1);

            // Try to create duplicate mapping with different identity
            var secondMapping = sut.MapIdentityPublic(key, identity2);

            // Should return the original mapping, not create a new one
            Assert.That(secondMapping.ExternalKey, Is.EqualTo(firstMapping.ExternalKey));
            Assert.That(secondMapping.AggregateId.AsString(), Is.EqualTo(firstMapping.AggregateId.AsString()));
            Assert.That(secondMapping.AggregateId.AsString(), Is.EqualTo(identity1.AsString())); // Original identity preserved
        }

        [Test]
        public void Verify_map_identity_normalizes_key_to_lowercase()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = new Guid().ToString().ToUpper() + "MIXED_Case";
            var identity = _identityManager.New<SampleAggregateId>();

            var mappedIdentity = sut.MapIdentityPublic(key, identity);

            Assert.That(mappedIdentity.ExternalKey, Is.EqualTo(key.ToLowerInvariant()));
        }

        [Test]
        public void Verify_map_identity_handles_concurrent_inserts()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var identity1 = _identityManager.New<SampleAggregateId>();
            var identity2 = _identityManager.New<SampleAggregateId>();

            // Create first mapping
            var firstMapping = sut.MapIdentityPublic(key, identity1);

            // Simulate concurrent insert by trying to insert the same key again
            // This should return the existing mapping due to duplicate key handling
            var concurrentMapping = sut.MapIdentityPublic(key, identity2);

            Assert.That(concurrentMapping.ExternalKey, Is.EqualTo(firstMapping.ExternalKey));
            Assert.That(concurrentMapping.AggregateId.AsString(), Is.EqualTo(firstMapping.AggregateId.AsString()));
        }

        [Test]
        public void Verify_map_identity_handles_empty_string_key()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = "";
            var identity = _identityManager.New<SampleAggregateId>();

            var mappedIdentity = sut.MapIdentityPublic(key, identity);

            Assert.That(mappedIdentity, Is.Not.Null);
            Assert.That(mappedIdentity.ExternalKey, Is.EqualTo(""));
            Assert.That(mappedIdentity.AggregateId.AsString(), Is.EqualTo(identity.AsString()));
        }

        [Test]
        public void Verify_map_identity_handles_whitespace_key()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = "  TRIM_ME  ";
            var identity = _identityManager.New<SampleAggregateId>();

            var mappedIdentity = sut.MapIdentityPublic(key, identity);

            Assert.That(mappedIdentity, Is.Not.Null);
            Assert.That(mappedIdentity.ExternalKey, Is.EqualTo(key.ToLowerInvariant()));
        }

        [Test]
        public void Verify_map_identity_handles_special_characters()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = "test@example.com#special$chars%123";
            var identity = _identityManager.New<SampleAggregateId>();

            var mappedIdentity = sut.MapIdentityPublic(key, identity);

            Assert.That(mappedIdentity, Is.Not.Null);
            Assert.That(mappedIdentity.ExternalKey, Is.EqualTo(key.ToLowerInvariant()));
            Assert.That(mappedIdentity.AggregateId.AsString(), Is.EqualTo(identity.AsString()));
        }

        [Test]
        public void Verify_map_identity_preserves_original_identity_on_collision()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            String key = Guid.NewGuid().ToString();
            var originalIdentity = _identityManager.New<SampleAggregateId>();
            var newIdentity = _identityManager.New<SampleAggregateId>();

            // Create original mapping
            var originalMapping = sut.MapIdentityPublic(key, originalIdentity);

            // Attempt to map same key to different identity
            var duplicateMapping = sut.MapIdentityPublic(key, newIdentity);

            // Should preserve original identity, not the new one
            Assert.That(duplicateMapping.AggregateId.AsString(), Is.EqualTo(originalIdentity.AsString()));
            Assert.That(duplicateMapping.AggregateId.AsString(), Is.Not.EqualTo(newIdentity.AsString()));

            // Verify the mapping still works correctly
            var retrievedIdentity = sut.MapWithWithoutAutomaticCreate(key);
            Assert.That(retrievedIdentity.AsString(), Is.EqualTo(originalIdentity.AsString()));
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

            public SampleAggregateId TryTranslateKey(String key)
            {
                return TryTranslate(key);
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

            public void ReplaceAliasForKey(SampleAggregateId key, string alias)
            {
                ReplaceAlias(key, alias);
            }

            public void DeleteAliasesForKey(SampleAggregateId key)
            {
                DeleteAliases(key);
            }

            public MappedIdentity MapIdentityPublic(String key, SampleAggregateId identity)
            {
                return MapIdentity(key, identity);
            }
        }
    }
}

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

        #region Batch Translation Tests

        [Test]
        public void Verify_translate_batch_creates_missing_mappings()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var key1 = Guid.NewGuid().ToString();
            var key2 = Guid.NewGuid().ToString();
            var key3 = Guid.NewGuid().ToString();

            var result = sut.TranslateBatchWithAutoCreate(new[] { key1, key2, key3 });

            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result.ContainsKey(key1), Is.True);
            Assert.That(result.ContainsKey(key2), Is.True);
            Assert.That(result.ContainsKey(key3), Is.True);

            // Verify all identities are different
            Assert.That(result[key1].AsString(), Is.Not.EqualTo(result[key2].AsString()));
            Assert.That(result[key2].AsString(), Is.Not.EqualTo(result[key3].AsString()));
            Assert.That(result[key1].AsString(), Is.Not.EqualTo(result[key3].AsString()));

            // Verify mappings were actually created in the database
            var verifyResult = sut.GetMultipleMapWithoutAutoCreation(key1, key2, key3);
            Assert.That(verifyResult.Count, Is.EqualTo(3));
            Assert.That(verifyResult[key1].AsString(), Is.EqualTo(result[key1].AsString()));
            Assert.That(verifyResult[key2].AsString(), Is.EqualTo(result[key2].AsString()));
            Assert.That(verifyResult[key3].AsString(), Is.EqualTo(result[key3].AsString()));
        }

        [Test]
        public void Verify_translate_batch_with_existing_mappings()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var key1 = Guid.NewGuid().ToString();
            var key2 = Guid.NewGuid().ToString();
            var key3 = Guid.NewGuid().ToString();

            // Create some mappings first
            var id1 = sut.MapWithAutomaticCreate(key1);
            var id2 = sut.MapWithAutomaticCreate(key2);

            // Now translate batch with mix of existing and new keys
            var result = sut.TranslateBatchWithAutoCreate(new[] { key1, key2, key3 });

            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[key1].AsString(), Is.EqualTo(id1.AsString()));
            Assert.That(result[key2].AsString(), Is.EqualTo(id2.AsString()));
            Assert.That(result.ContainsKey(key3), Is.True);
            Assert.That(result[key3], Is.Not.Null);
        }

        [Test]
        public void Verify_translate_batch_without_create_only_returns_existing()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var key1 = Guid.NewGuid().ToString();
            var key2 = Guid.NewGuid().ToString();
            var key3 = Guid.NewGuid().ToString();

            // Create only two mappings
            var id1 = sut.MapWithAutomaticCreate(key1);
            var id2 = sut.MapWithAutomaticCreate(key2);

            // Translate batch without auto-create
            var result = sut.TranslateBatchWithoutAutoCreate(new[] { key1, key2, key3 });

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[key1].AsString(), Is.EqualTo(id1.AsString()));
            Assert.That(result[key2].AsString(), Is.EqualTo(id2.AsString()));
            Assert.That(result.ContainsKey(key3), Is.False);

            // Verify key3 was not created in the database
            var verifyResult = sut.GetMultipleMapWithoutAutoCreation(key3);
            Assert.That(verifyResult.Count, Is.EqualTo(0));
        }

        [Test]
        public void Verify_try_translate_batch_returns_only_existing()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var key1 = Guid.NewGuid().ToString();
            var key2 = Guid.NewGuid().ToString();
            var key3 = Guid.NewGuid().ToString();

            // Create only two mappings
            var id1 = sut.MapWithAutomaticCreate(key1);
            var id2 = sut.MapWithAutomaticCreate(key2);

            // Try translate batch
            var result = sut.TryTranslateBatchKeys(new[] { key1, key2, key3 });

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[key1].AsString(), Is.EqualTo(id1.AsString()));
            Assert.That(result[key2].AsString(), Is.EqualTo(id2.AsString()));
            Assert.That(result.ContainsKey(key3), Is.False);

            // Verify key3 was not created
            var verifyResult = sut.GetMultipleMapWithoutAutoCreation(key3);
            Assert.That(verifyResult.Count, Is.EqualTo(0));
        }

        [Test]
        public void Verify_translate_batch_case_insensitive()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var keyLower = Guid.NewGuid().ToString().ToLower();
            var keyUpper = keyLower.ToUpper();
            var keyMixed = keyLower.Substring(0, keyLower.Length / 2).ToUpper() + 
                          keyLower.Substring(keyLower.Length / 2).ToLower();

            // Translate with different casings of the same key
            var result = sut.TranslateBatchWithAutoCreate(new[] { keyLower, keyUpper, keyMixed });

            // Should only create one mapping (case-insensitive)
            Assert.That(result.Count, Is.EqualTo(1));
            
            // All variations should map to the same identity
            var identityValue = result.Values.First().AsString();
            Assert.That(result[keyLower].AsString(), Is.EqualTo(identityValue));

            // Verify only one record was created in the database
            var verifyResult = sut.GetMultipleMapWithoutAutoCreation(keyLower, keyUpper, keyMixed);
            Assert.That(verifyResult.Count, Is.EqualTo(1));
        }

        [Test]
        public void Verify_translate_batch_with_null_throws()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var key1 = Guid.NewGuid().ToString();
            var key2 = Guid.NewGuid().ToString();

            // Include null in the batch - should throw
            Assert.Throws<AggregateException>(() => sut.TranslateBatchWithAutoCreate(new[] { key1, null, key2 }));
        }

        [Test]
        public void Verify_translate_batch_empty_array_returns_empty()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);

            var result = sut.TranslateBatchWithAutoCreate(new string[] { });

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void Verify_translate_batch_null_array_returns_empty()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);

            var result = sut.TranslateBatchWithAutoCreate(null);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void Verify_translate_batch_with_duplicates_in_input()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var key1 = Guid.NewGuid().ToString();
            var key2 = Guid.NewGuid().ToString();

            // Include duplicate keys in the input
            var result = sut.TranslateBatchWithAutoCreate(new[] { key1, key2, key1, key2 });

            // Should only create two mappings
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.ContainsKey(key1), Is.True);
            Assert.That(result.ContainsKey(key2), Is.True);

            // Verify only two records were created
            var verifyResult = sut.GetMultipleMapWithoutAutoCreation(key1, key2);
            Assert.That(verifyResult.Count, Is.EqualTo(2));
        }

        [Test]
        public void Verify_translate_batch_handles_concurrent_inserts()
        {
            TestMapper sut1 = new TestMapper(_db, _identityManager);
            TestMapper sut2 = new TestMapper(_db, _identityManager);
            
            var key1 = Guid.NewGuid().ToString();
            var key2 = Guid.NewGuid().ToString();
            var key3 = Guid.NewGuid().ToString();

            // First translator creates one mapping
            sut1.MapWithAutomaticCreate(key1);

            // Simulate concurrent batch operations from two different instances
            var result1 = sut1.TranslateBatchWithAutoCreate(new[] { key1, key2, key3 });
            var result2 = sut2.TranslateBatchWithAutoCreate(new[] { key1, key2, key3 });

            // Both should return all three mappings
            Assert.That(result1.Count, Is.EqualTo(3));
            Assert.That(result2.Count, Is.EqualTo(3));

            // The identities for the same keys should match across both results
            Assert.That(result1[key1].AsString(), Is.EqualTo(result2[key1].AsString()));
            Assert.That(result1[key2].AsString(), Is.EqualTo(result2[key2].AsString()));
            Assert.That(result1[key3].AsString(), Is.EqualTo(result2[key3].AsString()));

            // Verify no duplicate records were created
            var verifyResult = sut1.GetMultipleMapWithoutAutoCreation(key1, key2, key3);
            Assert.That(verifyResult.Count, Is.EqualTo(3));
        }

        [Test]
        public void Verify_translate_batch_large_set()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            
            // Create a large batch of keys
            var keys = Enumerable.Range(1, 100)
                .Select(i => Guid.NewGuid().ToString())
                .ToList();

            var result = sut.TranslateBatchWithAutoCreate(keys);

            Assert.That(result.Count, Is.EqualTo(100));
            
            // Verify all keys were mapped
            foreach (var key in keys)
            {
                Assert.That(result.ContainsKey(key), Is.True);
                Assert.That(result[key], Is.Not.Null);
            }

            // Verify all identities are unique
            var uniqueIds = result.Values.Select(v => v.AsString()).Distinct().Count();
            Assert.That(uniqueIds, Is.EqualTo(100));
        }

        [Test]
        public void Verify_translate_batch_idempotent()
        {
            TestMapper sut = new TestMapper(_db, _identityManager);
            var key1 = Guid.NewGuid().ToString();
            var key2 = Guid.NewGuid().ToString();

            // First batch translate
            var result1 = sut.TranslateBatchWithAutoCreate(new[] { key1, key2 });

            // Second batch translate with same keys
            var result2 = sut.TranslateBatchWithAutoCreate(new[] { key1, key2 });

            // Should return the same identities
            Assert.That(result1[key1].AsString(), Is.EqualTo(result2[key1].AsString()));
            Assert.That(result1[key2].AsString(), Is.EqualTo(result2[key2].AsString()));
        }

        #endregion

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

            public IReadOnlyDictionary<String, SampleAggregateId> GetMultipleMapWithoutAutoCreation(params String[] keys)
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

            public IReadOnlyDictionary<String, SampleAggregateId> TranslateBatchWithAutoCreate(IEnumerable<String> keys)
            {
                return TranslateBatchAsync(keys, createOnMissing: true).Result;
            }

            public IReadOnlyDictionary<String, SampleAggregateId> TranslateBatchWithoutAutoCreate(IEnumerable<String> keys)
            {
                return TranslateBatchAsync(keys, createOnMissing: false).Result;
            }

            public IReadOnlyDictionary<String, SampleAggregateId> TryTranslateBatchKeys(IEnumerable<String> keys)
            {
                return TryTranslateBatchAsync(keys).Result;
            }
        }
    }
}

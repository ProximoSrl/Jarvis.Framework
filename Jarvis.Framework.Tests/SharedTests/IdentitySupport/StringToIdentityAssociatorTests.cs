using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Bson;
using Jarvis.Framework.TestHelpers;
using Castle.Core.Logging;
using System.Reflection;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
	[TestFixture]
	public class StringToIdentityAssociatorTestsBase
	{
		protected IMongoDatabase _db;

		protected const string key1 = "k1";
		protected const string key1DifferentCasing = "K1";
		protected const string key2 = "k2";
		protected readonly TestId id1 = new TestId(1);
		protected readonly TestId id2 = new TestId(2);
		protected const string CollectionName = "TestIdentityAssociation";
		protected ILogger testLogger = NullLogger.Instance;

		protected class TestIdentityAssociation : MongoStringToIdentityAssociator<TestId>
		{
			public TestIdentityAssociation(IMongoDatabase database, ILogger testLogger, Boolean allowDuplicateId, Boolean caseInsensitive)
				: base(database, CollectionName, testLogger, allowDuplicateId, caseInsensitive)
			{
			}
		}

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            TestHelper.RegisterSerializerForFlatId<TestId>();
            TestHelper.RegisterSerializerForFlatId<TestFlatId>();
            _db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            var identityManager = new IdentityManager(new CounterService(_db));
            identityManager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
        }
    }

	[TestFixture]
	public class VerifyMigrationCaseInsensitive : StringToIdentityAssociatorTestsBase
	{
		private TestIdentityAssociation sut;
		private IMongoCollection<BsonDocument> _collection;

		[SetUp]
		public void SetUp()
		{
			_db.Drop();
			//Want case insensitive.
			_collection = _db.GetCollection<BsonDocument>(StringToIdentityAssociatorTestsBase.CollectionName);
		}

		[Test]
		public void verify_migration_base()
		{
			_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""a200"",
                ""Id"" : ""Test_1""
            }"));
			_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""A300"",
                ""Id"" : ""Test_2""
            }"));

			sut = new TestIdentityAssociation(_db, testLogger, true, true);

			//Database should be converted.
			var document = _collection.FindOneById("a200");
			Assert.That(document["OriginalKey"].AsString, Is.EqualTo("a200"));

			document = _collection.FindOneById("a300");
			Assert.That(document["OriginalKey"].AsString, Is.EqualTo("A300"));

			Assert.That(_collection.Count(Builders<BsonDocument>.Filter.Empty), Is.EqualTo(2L));
		}

		[Test]
		public void verify_migration_ignore_duplication_errors()
		{
			using (TestLogger.GlobalEnableStartScope())
			{
				_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""A200"",
                ""Id"" : ""Test_1""
            }"));
				_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""A300"",
                ""Id"" : ""Test_2""
            }"));
				///This error conflict with the previous one. is an error we cannot fix.
				_collection.InsertOne(BsonDocument.Parse(@"{ 
                ""_id"" : ""a300"",
                ""Id"" : ""Test_3""
            }"));

				TestLogger testLogger = new TestLogger();
				sut = new TestIdentityAssociation(_db, testLogger, true, true);

				//Database should be converted.
				var document = _collection.FindOneById("a200");
				Assert.That(document["OriginalKey"].AsString, Is.EqualTo("A200"));

				//The record with A300 cannot be converted, because it will conflict with the a300 lowercase. This can be 
				//a problem, but we cannot do anything.
				document = _collection.FindOneById("a300");
				Assert.That(document["OriginalKey"].AsString, Is.EqualTo("a300"));
				Assert.That(document["Id"].AsString, Is.EqualTo("Test_3"));

				document = _collection.FindOneById("A300");
				Assert.That(document["OriginalKey"].AsString, Is.EqualTo("A300"));
				Assert.That(document["Id"].AsString, Is.EqualTo("Test_2"));

				Assert.That(testLogger.ErrorCount, Is.EqualTo(1)); //We need to log the error

				Assert.That(_collection.Count(Builders<BsonDocument>.Filter.Empty), Is.EqualTo(3L));
			}
		}

		[Test]
		public void verify_migration_ignore_duplication_errors_reverse_error()
		{
			using (TestLogger.GlobalEnableStartScope())
			{
				_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""A200"",
                ""Id"" : ""Test_1""
            }"));

				///This error conflict with the following one. is an error we cannot fix.
				_collection.InsertOne(BsonDocument.Parse(@"{ 
                ""_id"" : ""a300"",
                ""Id"" : ""Test_3""
            }"));

				_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""A300"",
                ""Id"" : ""Test_2""
            }"));

				TestLogger testLogger = new TestLogger();
				sut = new TestIdentityAssociation(_db, testLogger, true, true);

				//Database should be converted.
				var document = _collection.FindOneById("a200");
				Assert.That(document["OriginalKey"].AsString, Is.EqualTo("A200"));

				//The record with A300 cannot be converted, because it will conflict with the a300 lowercase. This can be 
				//a problem, but we cannot do anything.
				document = _collection.FindOneById("a300");
				Assert.That(document["OriginalKey"].AsString, Is.EqualTo("a300"));
				Assert.That(document["Id"].AsString, Is.EqualTo("Test_3"));

				document = _collection.FindOneById("A300");
				Assert.That(document["OriginalKey"].AsString, Is.EqualTo("A300"));
				Assert.That(document["Id"].AsString, Is.EqualTo("Test_2"));

				Assert.That(testLogger.ErrorCount, Is.EqualTo(1)); //We need to log the error
				Assert.That(_collection.Count(Builders<BsonDocument>.Filter.Empty), Is.EqualTo(3L));
			}
		}
	}

	[TestFixture]
	public class VerifyMigrationCaseSensitive : StringToIdentityAssociatorTestsBase
	{
		private TestIdentityAssociation sut;
		private IMongoCollection<BsonDocument> _collection;

		[SetUp]
		public void SetUp()
		{
			_db.Drop();
			//Want case insensitive.
			_collection = _db.GetCollection<BsonDocument>(StringToIdentityAssociatorTestsBase.CollectionName);
		}

		[Test]
		public void verify_migration_base()
		{
			_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""a200"",
                ""Id"" : ""Test_1""
            }"));
			_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""A300"",
                ""Id"" : ""Test_2""
            }"));

			sut = new TestIdentityAssociation(_db, testLogger, true, false);

			//Database should be converted.
			var document = _collection.FindOneById("a200");
			Assert.That(document["OriginalKey"].AsString, Is.EqualTo("a200"));

			document = _collection.FindOneById("A300");
			Assert.That(document["OriginalKey"].AsString, Is.EqualTo("A300"));

			Assert.That(_collection.Count(Builders<BsonDocument>.Filter.Empty), Is.EqualTo(2L));
		}

		[Test]
		public void verify_migration_ignore_duplication_errors()
		{
			_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""A200"",
                ""Id"" : ""Test_1""
            }"));
			_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""A300"",
                ""Id"" : ""Test_2""
            }"));
			///This error conflict with the previous one. is an error we cannot fix.
			_collection.InsertOne(BsonDocument.Parse(@"{ 
                ""_id"" : ""a300"",
                ""Id"" : ""Test_3""
            }"));

			TestLogger testLogger = new TestLogger();
			sut = new TestIdentityAssociation(_db, testLogger, true, false);

			//Database should be converted.
			var document = _collection.FindOneById("A200");
			Assert.That(document["OriginalKey"].AsString, Is.EqualTo("A200"));

			//The record with A300 cannot be converted, because it will conflict with the a300 lowercase. This can be 
			//a problem, but we cannot do anything.
			document = _collection.FindOneById("a300");
			Assert.That(document["OriginalKey"].AsString, Is.EqualTo("a300"));
			Assert.That(document["Id"].AsString, Is.EqualTo("Test_3"));

			document = _collection.FindOneById("A300");
			Assert.That(document["OriginalKey"].AsString, Is.EqualTo("A300"));
			Assert.That(document["Id"].AsString, Is.EqualTo("Test_2"));

			Assert.That(testLogger.ErrorCount, Is.EqualTo(0)); //NO ERROR LOGGED
			Assert.That(_collection.Count(Builders<BsonDocument>.Filter.Empty), Is.EqualTo(3L));
		}

		[Test]
		public void verify_migration_ignore_duplication_errors_reverse_error()
		{
			_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""A200"",
                ""Id"" : ""Test_1""
            }"));

			///This error conflict with the following one. is an error we cannot fix.
			_collection.InsertOne(BsonDocument.Parse(@"{ 
                ""_id"" : ""a300"",
                ""Id"" : ""Test_3""
            }"));

			_collection.InsertOne(BsonDocument.Parse(@"{
                ""_id"" : ""A300"",
                ""Id"" : ""Test_2""
            }"));


			TestLogger testLogger = new TestLogger();
			sut = new TestIdentityAssociation(_db, testLogger, true, false);

			//Database should be converted.
			var document = _collection.FindOneById("A200");
			Assert.That(document["OriginalKey"].AsString, Is.EqualTo("A200"));

			//The record with A300 cannot be converted, because it will conflict with the a300 lowercase. This can be 
			//a problem, but we cannot do anything.
			document = _collection.FindOneById("a300");
			Assert.That(document["OriginalKey"].AsString, Is.EqualTo("a300"));
			Assert.That(document["Id"].AsString, Is.EqualTo("Test_3"));

			document = _collection.FindOneById("A300");
			Assert.That(document["OriginalKey"].AsString, Is.EqualTo("A300"));
			Assert.That(document["Id"].AsString, Is.EqualTo("Test_2"));

			Assert.That(testLogger.ErrorCount, Is.EqualTo(0)); //No log error
			Assert.That(_collection.Count(Builders<BsonDocument>.Filter.Empty), Is.EqualTo(3L));
		}
	}

	[TestFixture]
	public class StringToIdentityAssociatorTests : StringToIdentityAssociatorTestsBase
	{
		private TestIdentityAssociation sut;

		[SetUp]
		public void SetUp()
		{
			_db.Drop();
			sut = new TestIdentityAssociation(_db, testLogger, true, true);
		}

		[Test]
		public void verify_basic_mapping()
		{
			var result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
		}

		[Test]
		public void verify_throw_on_null_key()
		{
			Assert.Throws<ArgumentNullException>(() => sut.Associate(null, id1));
		}

		[Test]
		public void verify_throw_on_null_id()
		{
			Assert.Throws<ArgumentNullException>(() => sut.Associate(key1, null));
		}

		[Test]
		public void verify_detect_duplicate()
		{
			var result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
			result = sut.Associate(key1, id2);
			Assert.That(result, Is.False);
		}

		[Test]
		public void verify_duplicate_casing()
		{
			var result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
			result = sut.Associate(key1DifferentCasing, id2);
			Assert.That(result, Is.False, "Comparison of key should be case insensitive");
		}

		[Test]
		public void verify_key_casing_is_preserved()
		{
			var result = sut.Associate(key1.ToUpper(), id1);
			Assert.That(result, Is.True);
			var keyAssociated = sut.GetKeyFromId(id1);
			Assert.That(keyAssociated, Is.EqualTo(key1.ToUpper()));
		}

		[Test]
		public void verify_duplicate_of_id_allowed()
		{
			var result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
			result = sut.Associate(key2, id1);
			Assert.That(result, Is.True);
		}

		[Test]
		public void verify_multiple_association_is_idempotent()
		{
			var result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
			result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
		}

		[Test]
		public void verify_delete_association()
		{
			var result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
			sut.DeleteAssociationWithId(id1);
			var key = sut.GetKeyFromId(id1);
			Assert.That(key, Is.Null);
		}

		[Test]
		public void verify_delete_association_case_insensitive()
		{
			var result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
			sut.DeleteAssociationWithKey(key1DifferentCasing);
			var key = sut.GetKeyFromId(id1);
			Assert.That(key, Is.Null);
		}

		[Test]
		public void verify_get_id_from_key_case_insensitive()
		{
			var result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
			var id = sut.GetIdFromKey(key1DifferentCasing);
			Assert.That(id, Is.EqualTo(id1));
		}

		[Test]
		public void verify_delete_by_key()
		{
			var result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
			var count = sut.DeleteAssociationWithKey(key1);
			Assert.That(count, Is.EqualTo(1));
			var id = sut.GetIdFromKey(key1);
			Assert.That(id, Is.Null);
		}

		[Test]
		public void verify_update_associate_or_update_first_call()
		{
			var result = sut.AssociateOrUpdate(key1, id1);
			Assert.That(result, Is.True);
			var key = sut.GetKeyFromId(id1);
			Assert.That(key, Is.EqualTo(key1));
		}

		[Test]
		public void verify_update_associate_is_idempotent()
		{
			var result = sut.AssociateOrUpdate(key1, id1);
			Assert.That(result, Is.True);
			result = sut.AssociateOrUpdate(key1, id1);
			Assert.That(result, Is.True);

			var key = sut.GetKeyFromId(id1);
			Assert.That(key, Is.EqualTo(key1));
		}

		[Test]
		public void verify_update_update_association()
		{
			sut.Associate(key1, id1);
			var result = sut.AssociateOrUpdate(key2, id1);
			Assert.That(result, Is.True);
			var key = sut.GetKeyFromId(id1);
			Assert.That(key, Is.EqualTo(key2));
			var id = sut.GetIdFromKey(key1);
			Assert.That(id, Is.Null);
		}

		[Test]
		public void verify_associate_or_update_with_existing_key()
		{
			sut.Associate(key1, id1);
			sut.Associate(key2, id2);
			var result = sut.AssociateOrUpdate(key2, id1);

			//We should not change anything, because key2 was already associated to id1.
			var key = sut.GetKeyFromId(id1);
			Assert.That(key, Is.EqualTo(key1));
			key = sut.GetKeyFromId(id2);
			Assert.That(key, Is.EqualTo(key2));

			Assert.That(result, Is.False);
		}

		[Test]
		public void verify_update_or_associate_id_or_update_first_call()
		{
			var result = sut.AssociateOrUpdate(key1, id1);
			Assert.That(result, Is.True);
			var key = sut.GetKeyFromId(id1);
			Assert.That(key, Is.EqualTo(key1));
		}

		[Test]
		public void verify_update_or_associate_id_change_association_with_different_key()
		{
			sut.Associate(key1, id1);
			var result = sut.AssociateOrUpdate(key2, id1);
			Assert.That(result, Is.True);
			var key = sut.GetKeyFromId(id1);
			Assert.That(key, Is.EqualTo(key2));

			var id = sut.GetIdFromKey(key1);
			Assert.That(id, Is.Null);
		}
	}

	[TestFixture]
	public class StringToIdentityAssociatorTests_UniqueId : StringToIdentityAssociatorTestsBase
	{
		TestIdentityAssociation uniqueIdSut;

		[SetUp]
		public void SetUp()
		{
			_db.Drop();
			uniqueIdSut = new TestIdentityAssociation(_db, testLogger, false, true);
		}

		[Test]
		public void verify_detect_duplicate_of_id()
		{
			var result = uniqueIdSut.Associate(key1, id1); //The sut is different
			Assert.That(result, Is.True);
			result = uniqueIdSut.Associate(key2, id1);
			Assert.That(result, Is.False);
		}

		[Test]
		public void verify_associate_or_update_with_existing_key_uniqueId()
		{
			uniqueIdSut.Associate(key1, id1);
			uniqueIdSut.Associate(key2, id2);
			var result = uniqueIdSut.AssociateOrUpdate(key2, id1);

			var key = uniqueIdSut.GetKeyFromId(id1);
			Assert.That(key, Is.EqualTo(key1));
			key = uniqueIdSut.GetKeyFromId(id2);
			Assert.That(key, Is.EqualTo(key2));

			Assert.That(result, Is.False);
		}
	}

	[TestFixture]
	public class StringToIdentityAssociatorTests_CaseSensitive : StringToIdentityAssociatorTestsBase
	{
		TestIdentityAssociation sut;

		[SetUp]
		public void SetUp()
		{
			_db.Drop();
			sut = new TestIdentityAssociation(_db, testLogger, false, caseInsensitive: false);
		}

		[Test]
		public void verify_case_Sensitiveness_of_keys()
		{
			var result = sut.Associate(key1, id1);
			Assert.That(result, Is.True);
			result = sut.Associate(key1DifferentCasing, id2);
			Assert.That(result, Is.True, "Case is different, but this is case sensitive");
		}

		[Test]
		public void verify_retrieve_by_key()
		{
			sut.Associate(key1, id1);
			sut.Associate(key1DifferentCasing, id2);

			Assert.That(sut.GetIdFromKey(key1), Is.EqualTo(id1));
			Assert.That(sut.GetIdFromKey(key1DifferentCasing), Is.EqualTo(id2));
		}

		[Test]
		public void verify_retrieve_by_id_return_case_sensitive_key()
		{
			sut.Associate(key1, id1);
			sut.Associate(key1DifferentCasing, id2);

			Assert.That(sut.GetKeyFromId(id1), Is.EqualTo(key1));
			Assert.That(sut.GetKeyFromId(id2), Is.EqualTo(key1DifferentCasing));
		}

		[Test]
		public void verify_delete_by_key()
		{
			sut.Associate(key1, id1);
			sut.Associate(key1DifferentCasing, id2);
			sut.DeleteAssociationWithKey(key1);

			Assert.That(sut.GetIdFromKey(key1), Is.Null);
			Assert.That(sut.GetIdFromKey(key1DifferentCasing), Is.EqualTo(id2));
		}
	}
}

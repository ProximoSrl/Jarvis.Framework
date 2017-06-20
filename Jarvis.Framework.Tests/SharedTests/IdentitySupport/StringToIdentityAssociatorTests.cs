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
namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    [TestFixture]
    public class StringToIdentityAssociatorTests
    {
        TestIdentityAssociation sut;
        TestIdentityAssociationWithUniqueId uniqueIdSut;

        IMongoDatabase _db;

        private const string key1 = "k1";
        private const string key2 = "k2";
        private readonly TestId id1 = new TestId(1);
        private readonly TestId id2 = new TestId(2);

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
        }

        [SetUp]
        public void SetUp()
        {
            _db.Drop();
            sut = new TestIdentityAssociation(_db);
            uniqueIdSut = new TestIdentityAssociationWithUniqueId(_db);
        }


        [Test]
        public void verify_basic_mapping()
        {
            var result = sut.Associate(key1, id1);
            Assert.That(result, Is.True);
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
        public void verify_detect_duplicate_of_id()
        {
            var result = uniqueIdSut.Associate(key1, id1);
            Assert.That(result, Is.True);
            result = uniqueIdSut.Associate(key2, id1);
            Assert.That(result, Is.False);
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
        }

        private class TestIdentityAssociation : MongoStringToIdentityAssociator<TestId>
        {
            public TestIdentityAssociation(IMongoDatabase database)
                : base(database, "TestIdentity")
            {

            }

        }

        private class TestIdentityAssociationWithUniqueId : MongoStringToIdentityAssociator<TestId>
        {
            public TestIdentityAssociationWithUniqueId(IMongoDatabase database)
                : base(database, "TestIdentityUnique", false)
            {

            }

        }
    }
}

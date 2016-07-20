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
        public void verify_delete_by_id()
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
            sut.DeleteAssociationWithKey(key1);
            var id = sut.GetKeyFromKey(key1);
            Assert.That(id, Is.Null);
        }

        private class TestIdentityAssociation : StringToIdentityAssociator<TestId> 
        {
            public TestIdentityAssociation(IMongoDatabase database)
                :base (database, "TestIdentity")
            {
                
            }

        }

        private class TestIdentityAssociationWithUniqueId : StringToIdentityAssociator<TestId>
        {
            public TestIdentityAssociationWithUniqueId(IMongoDatabase database)
                : base(database, "TestIdentityUnique", false)
            {

            }

        }
    }
}

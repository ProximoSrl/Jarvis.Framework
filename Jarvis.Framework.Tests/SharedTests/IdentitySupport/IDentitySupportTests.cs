using Jarvis.Framework.Tests.Support;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    [TestFixture]
    [Category("mongo_serialization")]
    public class IDentitySupportTests
    {
        private TestMapper sut;
        private TestFlatMapper sutFlat;
        private IMongoCollection<BsonDocument> _mappingCollection;
        private IMongoCollection<BsonDocument> _mappingFlatCollection;
        private IdentityManager _identityManager;

        [OneTimeSetUp]
        public void TestFixtureSetup()
        {
            TestHelper.RegisterSerializerForFlatId<TestId>();
            TestHelper.RegisterSerializerForFlatId<TestFlatId>();
        }

        [SetUp]
        public void SetUp()
        {
            var db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            _identityManager = new IdentityManager(new CounterService(db));
            _identityManager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
            _mappingCollection = db.GetCollection<BsonDocument>("map_testid");
            _mappingFlatCollection = db.GetCollection<BsonDocument>("map_testflatid");
            sut = new TestMapper(db, _identityManager);
            sutFlat = new TestFlatMapper(db, _identityManager);
        }

        [Test]
        public void Verify_delete_of_mapping()
        {
            var id = sut.Map("TEST");
            var mapCount = _mappingCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(1));

            sut.DeleteAliases(id);
            mapCount = _mappingCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(0));
        }

        [Test]
        public void Verify_delete_of_flat_mapping()
        {
            var id = sutFlat.Map("TEST");
            var mapCount = _mappingFlatCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(1));

            sutFlat.DeleteAliases(id);
            mapCount = _mappingFlatCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(0));
        }

        [Test]
        public void Verify_replace_alias_of_flat_mapping()
        {
            var id = sutFlat.Map("TEST");
            var mapCount = _mappingFlatCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(1));

            sutFlat.ReplaceAlias(id, "TEST2");
            mapCount = _mappingFlatCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Verify_exception_of_multiple_map()
        {
            sut.Addalias(new TestId(2), "Alias2");
            try
            {
                sut.Addalias(new TestId(3), "Alias2");
                Assert.Fail("Expect exception for invalid alias");
            }
            catch (Exception ex)
            {
                Assert.That(ex.Message, Contains.Substring("Alias alias2 already mapped to Test_2"));
            }
        }

        [Test]
        public void Can_generate_new_abstract_identity()
        {
            var id = _identityManager.New<MyAbstractIdentityId>();
            Assert.That(id.Id, Is.GreaterThan(0L));
        }

        [Test]
        public void should_serialize_abstract_id()
        {
            var instance = new TestAbstractId(1);
            var json = instance.ToJson();

            Assert.AreEqual("\"TestAbstract_1\"", json);
        }

        [Test]
        public void should_serialize_class_with_abstract_id()
        {
            var instance = new WithAbstractId() { AbstractId = new TestAbstractId(42) };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"AbstractId\" : \"TestAbstract_42\" }", json);
        }

        [Test]
        public void should_deserialize_class_with_abstract_id()
        {
            var instance = BsonSerializer.Deserialize<WithAbstractId>("{ \"AbstractId\" : \"TestAbstract_42\" }");
            Assert.That(instance.AbstractId.Id, Is.EqualTo(42L));
        }
    }




}

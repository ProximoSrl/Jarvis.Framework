using Jarvis.Framework.Tests.Support;

namespace Jarvis.Framework.Tests.External.NStoreTests
{
    [TestFixture]
    public class NStoreSerializationTests
    {
        [Test]
        public void Verify_that_changeset_can_serialize_header_with_dots_correctly()
        {
            Changeset sut = new Changeset(12, new Object[0]);
            sut.Headers.Add("test.dot", "value");
            sut.Headers.Add("nodot", "value");

            var serialized = sut.ToJson();
            Console.WriteLine(serialized);
            Assert.That(serialized, Does.Contain("test.dot"));
        }

        [Test]
        public void Verify_that_we_can_save_changeset_with_headers()
        {
            var sut = new Changeset(12, new Object[0]);
            sut.Headers.Add("test.dot", "value");
            sut.Headers.Add("nodot", "value");

            var db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString);
            var collection = db.GetCollection<Changeset>("test_changeset");
            db.DropCollection(collection.CollectionNamespace.CollectionName);

            collection.InsertOne(sut);
        }
    }
}

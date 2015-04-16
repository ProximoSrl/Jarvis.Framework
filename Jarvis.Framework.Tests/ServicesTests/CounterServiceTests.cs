using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.IdentitySupport;
using MongoDB.Driver;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.ServicesTests
{
    [TestFixture]
    public class CounterServiceTests
    {
        private CounterService _service;
        private MongoDatabase _db;

        [SetUp]
        public void SetUp()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            var client = new MongoClient(url);
            _db = client.GetServer().GetDatabase(url.DatabaseName);
            _db.Drop();
            _service = new CounterService(_db);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _db.Drop();
        }

        [Test]
        public void create_first()
        {
            var first = _service.GetNext("test");
            Assert.AreEqual(1, first);
        }

        [Test]
        public void create_second()
        {
            _service.GetNext("test");
            var second = _service.GetNext("test");
            Assert.AreEqual(2, second);
        }

        [Test]
        public void paralle_test()
        {
            Parallel.ForEach(Enumerable.Range(1, 100), i => _service.GetNext("parallel"));
            var last = _service.GetNext("parallel");
            Assert.AreEqual(101, last);
        }
    }
}

using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.IdentitySupport;
using MongoDB.Driver;
using NUnit.Framework;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.ServicesTests
{
    [TestFixture]
    public class CounterServiceTests
    {
        private CounterService _service;
        private IMongoDatabase _db;

        [SetUp]
        public void SetUp()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
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

        /// <summary>
        /// this is somewhat empiric, but with Wired Tiger we have problems because sometimes
        /// it fails to insert the very first element for a sequence.
        /// </summary>
        [Test]
        public void parallel_test()
        {
            for (int j = 0; j < 100; j++)
            {
                //System.Console.WriteLine("Iteration " + j);
                _db.Drop();
                Parallel.ForEach(Enumerable.Range(1, 5), i => _service.GetNext("parallel"));
                var last = _service.GetNext("parallel");
            }

        }
    }
}

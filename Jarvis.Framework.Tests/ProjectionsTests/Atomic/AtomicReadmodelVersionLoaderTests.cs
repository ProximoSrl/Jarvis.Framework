using Castle.Core.Logging;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using MongoDB.Driver;
using NStore.Core.InMemory;
using NUnit.Framework;
using System.Configuration;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support
{
    [TestFixture]
    public class AtomicReadmodelVersionLoaderTests
    {
        private IMongoDatabase _db;
        protected IMongoCollection<SimpleTestAtomicReadModel> _collection;
        private InMemoryPersistence _persistence;
        private AtomicMongoCollectionWrapper<SimpleTestAtomicReadModel> _collectionWrapper;

        private AtomicReadModelVersionLoader CreateSut()
        {
            return new AtomicReadModelVersionLoader(_db);
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _db.Drop();

            _collection = _db.GetCollection<SimpleTestAtomicReadModel>(
                CollectionNames.GetCollectionName<SimpleTestAtomicReadModel>());

            _persistence = new InMemoryPersistence();
            _collectionWrapper = new AtomicMongoCollectionWrapper<SimpleTestAtomicReadModel>(
              _db,
              new AtomicReadModelFactory(),
              new LiveAtomicReadModelProcessor(new AtomicReadModelFactory(), new CommitEnhancer(), _persistence),
              NullLogger.Instance);
        }

        [SetUp]
        public void SetUp()
        {
            _db.Drop();
        }

        [Test]
        public void Verify_basic_get_for_non_existing_readmodel()
        {
            var sut = CreateSut();
            Assert.That(sut.CountReadModelToUpdateByName("Unexistingreamodel", 3), Is.EqualTo(0));
        }

        [Test]
        public async Task Verify_basic_get_for_readmodel()
        {
            SimpleTestAtomicReadModel.FakeSignature = 1;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(1));
            await _collectionWrapper.UpsertAsync(rm).ConfigureAwait(false);
            var sut = CreateSut();
            var name = CollectionNames.GetCollectionName(typeof(SimpleTestAtomicReadModel));
            Assert.That(sut.CountReadModelToUpdateByName(name, 1), Is.EqualTo(0));
        }

        [Test]
        public async Task Verify_basic_get_for_readmodel_when_signature_change()
        {
            SimpleTestAtomicReadModel.FakeSignature = 1;
            var rm = new SimpleTestAtomicReadModel(new SampleAggregateId(2));
            await _collectionWrapper.UpsertAsync(rm).ConfigureAwait(false);
            var sut = CreateSut();
            var name = CollectionNames.GetCollectionName(typeof(SimpleTestAtomicReadModel));
            Assert.That(sut.CountReadModelToUpdateByName(name, 2), Is.EqualTo(1));
        }
    }
}

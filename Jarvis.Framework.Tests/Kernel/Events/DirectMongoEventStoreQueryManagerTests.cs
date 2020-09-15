using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.Support;

namespace Jarvis.Framework.Tests.Kernel.Events
{
    [TestFixture]
    public class DirectMongoEventStoreQueryManagerTests
    {
        private DirectMongoEventStoreQueryManager sut;
        private Repository repository;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var db = TestHelper.GetEventStoreDb();
            db.DropCollection(EventStoreFactory.PartitionCollectionName);
            sut = new DirectMongoEventStoreQueryManager(db);
            repository = TestHelper.GetRepository();
        }

        private Int64 seed = 1;

        private SampleAggregateId GenerateId()
        {
            return new SampleAggregateId(seed++);
        }

        [Test]
        public async Task verify_basic_retrieval_of_commit_info()
        {
            SampleAggregateId id = GenerateId();
            Dictionary<String, String> headers = new Dictionary<string, string>()
            {
                ["test"] = "blah",
                ["anothertest"] = "blah blah"
            };

            await SaveAggregateWithHeaders(id, headers).ConfigureAwait(false);

            var element = await sut.GetCommitsAfterCheckpointTokenAsync(0, new List<string>() { id }).ConfigureAwait(false);
            Assert.That(element, Has.Count.EqualTo(1), "We have a single commit for the aggregate");
            Assert.That(element[0].Id, Is.Not.Null);
            Assert.That(element[0].PartitionId, Is.EqualTo(id.ToString()));
            Assert.That(element[0].Headers.Count, Is.EqualTo(2));
            Assert.That(element[0].Headers["test"], Is.EqualTo("blah"));
            Assert.That(element[0].Headers["anothertest"], Is.EqualTo("blah blah"));
        }

        [Test]
        public async Task verify_headers_with_null_value()
        {
            SampleAggregateId id = GenerateId();
            Dictionary<String, String> headers = new Dictionary<string, string>()
            {
                ["test"] = "blah",
                ["anothertest"] = null
            };

            await SaveAggregateWithHeaders(id, headers).ConfigureAwait(false);

            var element = await sut.GetCommitsAfterCheckpointTokenAsync(0, new List<string>() { id }).ConfigureAwait(false);
            Assert.That(element, Has.Count.EqualTo(1), "We have a single commit for the aggregate");
            Assert.That(element[0].Id, Is.Not.Null);
            Assert.That(element[0].PartitionId, Is.EqualTo(id.ToString()));
            Assert.That(element[0].Headers.Count, Is.EqualTo(2));
            Assert.That(element[0].Headers["test"], Is.EqualTo("blah"));
            Assert.That(element[0].Headers["anothertest"], Is.EqualTo(null));
        }

        [Test]
        public async Task verify_filtering_for_id_list()
        {
            SampleAggregateId id1 = GenerateId();
            SampleAggregateId id2 = GenerateId();
            SampleAggregateId id3 = GenerateId();
            await SaveAggregateWithHeaders(id1, null).ConfigureAwait(false);
            await SaveAggregateWithHeaders(id2, null).ConfigureAwait(false);
            await SaveAggregateWithHeaders(id3, null).ConfigureAwait(false);

            var returnValue = await sut.GetCommitsAfterCheckpointTokenAsync(0, new List<string>() { id2, id3 }).ConfigureAwait(false);
            Assert.That(returnValue, Has.Count.EqualTo(2), "We have a single commit for the aggregate");
            Assert.That(returnValue.Any(e => e.PartitionId == id2));
            Assert.That(returnValue.Any(e => e.PartitionId == id3));
        }

        private async Task SaveAggregateWithHeaders(SampleAggregateId id, Dictionary<string, string> headers)
        {
            var sampleAggregate = await repository.GetByIdAsync<SampleAggregate>(id).ConfigureAwait(false);
            sampleAggregate.Create();
            sampleAggregate.Touch();

            await repository.SaveAsync(sampleAggregate, Guid.NewGuid().ToString(), a =>
            {
                if (headers != null)
                {
                    foreach (var h in headers)
                    {
                        a.Add(h.Key, h.Value);
                    }
                }
            }).ConfigureAwait(false);
        }
    }
}

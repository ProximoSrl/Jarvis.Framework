using Jarvis.Framework.Tests.EngineTests;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
    public class ProjectionEngineBasicTestBase : AbstractV2ProjectionEngineTests
    {
        public ProjectionEngineBasicTestBase(String pollingClientVersion) : base(pollingClientVersion)
        {

        }

        protected override void RegisterIdentities(IdentityManager identityConverter)
        {
            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);
        }

        protected override string GetConnectionString()
        {
            return ConfigurationManager.ConnectionStrings["engine"].ConnectionString;
        }

        protected override IEnumerable<IProjection> BuildProjections()
        {
            var writer = new CollectionWrapper<SampleReadModel, string>(StorageFactory, new NotifyToNobody());
            yield return new Projection(writer);
        }
    }

    //[TestFixture("1")]
    [TestFixture("2")]
    public class ProjectionEngineTestBasic : ProjectionEngineBasicTestBase
    {
        public ProjectionEngineTestBasic(String pollingClientVersion) : base(pollingClientVersion)
        {

        }

        [Test]
        public async Task run_poll_and_wait()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
            Thread.Sleep(50);
            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            Assert.AreEqual(1, reader.AllSortedById.Count());
        }
    }

    [TestFixture("2")]
    public class ProjectionEngineWithRebuild : ProjectionEngineBasicTestBase
    {
        public ProjectionEngineWithRebuild(String pollingClientVersion) : base(pollingClientVersion)
        {

        }

        protected override bool OnShouldUseNitro()
        {
            return true;
        }

        [Test]
        public async Task start_with_rebuild_then_stop_rebuild()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
            Thread.Sleep(50);
            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            Assert.AreEqual(1, reader.AllSortedById.Count());
            var checkpoint = _checkpoints.FindOneById("Projection");
            Assert.That(checkpoint.Value, Is.EqualTo(1), "Checkpoint is written after rebuild.");
        }
    }
}

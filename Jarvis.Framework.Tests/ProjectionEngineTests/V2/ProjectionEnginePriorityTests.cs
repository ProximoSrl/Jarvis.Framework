using Jarvis.Framework.Tests.EngineTests;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
    //[TestFixture("1")]
    [TestFixture("2")]
    public class ProjectionEnginePriorityTests : AbstractV2ProjectionEngineTests
    {

        public ProjectionEnginePriorityTests(String pollingClientVersion) : base(pollingClientVersion)
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
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(StorageFactory, new NotifyToNobody());

            yield return new Projection(writer);
            yield return new Projection2(writer2);
        }

        [Test]
        public async Task verify_priority_is_maintained()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var reader2 = new MongoReader<SampleReadModel2, string>(Database);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
            Thread.Sleep(100);
            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);

            var rm = reader.AllUnsorted.Single();
            var rm2 = reader2.AllUnsorted.Single();
            Assert.That(rm2.Timestamp, Is.LessThan(rm.Timestamp));
        }
    }
}

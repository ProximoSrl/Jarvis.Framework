using Jarvis.Framework.Tests.EngineTests;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
    //[TestFixture("1")]
    [TestFixture("2")]
    public class ProjectionEngineTestsCheckpoints : ProjectionEngineBasicTestBase
    {
        public ProjectionEngineTestsCheckpoints(String pollingClientVersion) : base(pollingClientVersion)
        {

        }

        private readonly Boolean returnProjection3 = true;

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
            var writer3 = new CollectionWrapper<SampleReadModel3, string>(StorageFactory, new NotifyToNobody());
            if (returnProjection3) yield return new Projection3(writer3);
        }

        [Test]
        public async Task run_poller()
        {
            var projected = _statusChecker.IsCheckpointProjectedByAllProjection(2);
            if (projected)
            {
                var allProjected = _checkpoints.FindAll().ToList();
                foreach (var checkpoint in allProjected)
                {
                    Console.WriteLine("Projection {0} at checkpoint {1}",
                        checkpoint.Signature, checkpoint.Current);
                }
            }
            Console.WriteLine("FIRST CHECK DONE");
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).Wait();

            aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(2)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

            var lastPosition = await GetLastPositionAsync().ConfigureAwait(false);

            //need to wait for at least one checkpoint written to database.
            DateTime startTime = DateTime.Now;
            while (!_checkpoints.FindAll().Any()
                && DateTime.Now.Subtract(startTime).TotalMilliseconds < 2000) //2 seconds timeout is fine
            {
                Thread.Sleep(100);
            }

            projected = _statusChecker.IsCheckpointProjectedByAllProjection(lastPosition);
            if (projected)
            {
                var allProjected = _checkpoints.FindAll().ToList();
                foreach (var checkpoint in allProjected)
                {
                    Console.WriteLine("Projection {0} at checkpoint {1}",
                        checkpoint.Signature, checkpoint.Current);
                }
            }
            Assert.That(projected, Is.False);

            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            Assert.That(_statusChecker.IsCheckpointProjectedByAllProjection(lastPosition), Is.True);
        }
    }
}
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Tests.EngineTests;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
    [TestFixture("2")]
    public class ProjectionStatusLoaderTest : ProjectionEngineBasicTestBase
    {
        public ProjectionStatusLoaderTest(String pollingClientVersion) : base(pollingClientVersion)
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
            var writer3 = new CollectionWrapper<SampleReadModel3, string>(StorageFactory, new NotifyToNobody());
            yield return new Projection3(writer3);
        }

        [Test]
        public async Task Verify_aggregation_call()
        {
            ProjectionStatusLoader sut = new ProjectionStatusLoader(Database, Database, 5);

            SampleAggregateId identity1 = new SampleAggregateId(1);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(identity1).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate,Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

            SampleAggregateId identity2 = new SampleAggregateId(2);
            aggregate = await Repository.GetByIdAsync<SampleAggregate>(identity2).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate,Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            await FlushCheckpointCollectionAsync().ConfigureAwait(false);

            var result = sut.GetSlotMetrics();
            Assert.That(result.Count(), Is.EqualTo(2));
            Assert.That(result.ElementAt(0).CommitBehind, Is.EqualTo(0));
            Assert.That(result.ElementAt(0).CommitBehind, Is.EqualTo(0));
        }
    }
}

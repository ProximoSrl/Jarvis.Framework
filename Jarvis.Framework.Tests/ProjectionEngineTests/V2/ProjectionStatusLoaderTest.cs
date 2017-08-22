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

    //[TestFixture("1")]
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
        public async void Verify_aggregation_call()
        {
            ProjectionStatusLoader sut = new ProjectionStatusLoader(Database, Database, 5);

            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregateId(1));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });

            aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregateId(2));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });

            var stream = _eventStore.Advanced.GetFrom(0);
            var lastCommit = stream.Last();
            await Engine.UpdateAndWait();

            var result = sut.GetSlotMetrics();
            Assert.That(result.Count(), Is.EqualTo(2));
            Assert.That(result.ElementAt(0).CommitBehind, Is.EqualTo(0));
            Assert.That(result.ElementAt(0).CommitBehind, Is.EqualTo(0));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Tests.EngineTests;

using NUnit.Framework;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{

    //[TestFixture("1")]
    [TestFixture("2")]
    public class ProjectionEngineTestsRemovedProjection : ProjectionEngineBasicTestBase
    {
        public ProjectionEngineTestsRemovedProjection(String pollingClientVersion) : base(pollingClientVersion)
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
            if (returnProjection3) yield return new Projection3(writer3);
        }

        private Boolean returnProjection3 = true;

        [Test]
        public async void verify_projection_removed()
        {
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregateId(1));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });

            aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregateId(2));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });

            var stream = _eventStore.Advanced.GetFrom(0);
            var lastCommit = stream.Last();
            await Engine.UpdateAndWait();
            Assert.That(_statusChecker.IsCheckpointProjectedByAllProjection(lastCommit.CheckpointToken), Is.True);

            //now projection 3 is not returned anymore, it simulates a projection that is no more active
            returnProjection3 = false;
            ConfigureEventStore();
            ConfigureProjectionEngine();

            aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregateId(3));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });

            stream = _eventStore.Advanced.GetFrom(0);
            lastCommit = stream.Last();
            await Engine.UpdateAndWait();
            Assert.That(_statusChecker.IsCheckpointProjectedByAllProjection(lastCommit.CheckpointToken), Is.True);

        }

    }
}
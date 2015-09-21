using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.Concurrent
{
    [TestFixture]
    public class ProjectionEngineTestsCheckpoints : AbstractConcurrentProjectionEngineTests
    {
        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
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
            var writer = new CollectionWrapper<SampleReadModel, string>(StorageFactory,new NotifyToNobody());
            yield return new Projection(writer);
            var writer3 = new CollectionWrapper<SampleReadModel3, string>(StorageFactory, new NotifyToNobody());
            if (returnProjection3) yield return new Projection3(writer3);
        }
        private Boolean returnProjection3 = true;

        [Test]
        public async void run_poller()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregateId(1));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });

            aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregateId(2));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });

            var stream = _eventStore.Advanced.GetFrom("0");
            var lastCommit = stream.Last();

            Assert.That(_statusChecker.IsCheckpointProjectedByAllProjection(lastCommit.CheckpointToken), Is.False);
             
            await Engine.UpdateAndWait();
            Assert.That(_statusChecker.IsCheckpointProjectedByAllProjection(lastCommit.CheckpointToken), Is.True);
        }

    
    }
}
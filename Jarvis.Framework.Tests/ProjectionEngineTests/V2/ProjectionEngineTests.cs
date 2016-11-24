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
using Jarvis.Framework.Shared.Helpers;
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
        public async void run_poll_and_wait()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregateId(1));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });
            Thread.Sleep(50);
            await Engine.UpdateAndWait();
            Assert.AreEqual(1, reader.AllSortedById.Count());
        }
    }


    //[TestFixture("1")]
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
        public async void start_with_rebuild_then_stop_rebuild()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregateId(1));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });
            Thread.Sleep(50);
            await Engine.UpdateAndWait();
            Assert.AreEqual(1, reader.AllSortedById.Count());
            var checkpoint = _checkpoints.FindOneById("Projection");
            Assert.That(checkpoint.Value, Is.EqualTo(1), "Checkpoint is written after rebuild.");
        }
    }


}

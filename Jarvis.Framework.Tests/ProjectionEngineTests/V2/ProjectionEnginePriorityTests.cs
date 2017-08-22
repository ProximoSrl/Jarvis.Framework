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
            var writer = new CollectionWrapper<SampleReadModel, string>(StorageFactory,new NotifyToNobody());
            var writer2 = new CollectionWrapper<SampleReadModel2, string>(StorageFactory, new NotifyToNobody());

            yield return new Projection(writer);
            yield return new Projection2(writer2);
        }

        [Test]
        public async void verify_priority_is_maintained()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var reader2 = new MongoReader<SampleReadModel2, string>(Database);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregateId(1));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });
            Thread.Sleep(100);
            await Engine.UpdateAndWait();

            var rm = reader.AllUnsorted.Single();
            var rm2 = reader2.AllUnsorted.Single();
            Assert.That(rm2.Timestamp, Is.LessThan(rm.Timestamp));
        }
    }
}

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
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
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
        public async Task Verify_projection_removed()
        {
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate,Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

            aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(2)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate,Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

            var lastPosition = await GetLastPositionAsync().ConfigureAwait(false);

            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            await FlushCheckpointCollectionAsync().ConfigureAwait(false);

            Assert.That(await _statusChecker.IsCheckpointProjectedByAllProjectionAsync(lastPosition).ConfigureAwait(false), Is.True);

            //now projection 3 is not returned anymore, it simulates a projection that is no more active
            returnProjection3 = false;
            ConfigureEventStore();
            await ConfigureProjectionEngineAsync().ConfigureAwait(false);

            aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(3)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate,Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

            lastPosition = await GetLastPositionAsync().ConfigureAwait(false);
            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            Assert.That(await _statusChecker.IsCheckpointProjectedByAllProjectionAsync(lastPosition).ConfigureAwait(false), Is.True);
        }
    }
}
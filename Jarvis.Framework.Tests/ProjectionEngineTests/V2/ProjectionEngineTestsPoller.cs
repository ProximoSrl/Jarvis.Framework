using Fasterflect;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
    [TestFixture("2")]
    public class ProjectionEngineTestsPoller : ProjectionEngineBasicTestBase
    {
        public ProjectionEngineTestsPoller(String pollingClientVersion) : base(pollingClientVersion)
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

        [Test]
        public async Task run_poller()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1, reader.AllSortedById.Count());

            aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(2)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);

            NUnit.Framework.Legacy.ClassicAssert.AreEqual(2, reader.AllSortedById.Count());
        }
    }

    [TestFixture("2")]
    public class ProjectionEngineTestsPollerStartingCheckpoint : ProjectionEngineBasicTestBase
    {
        public ProjectionEngineTestsPollerStartingCheckpoint(String pollingClientVersion) : base(pollingClientVersion)
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

            var otherProjectionWriter = new CollectionWrapper<SampleReadModel3, string>(StorageFactory, new NotifyToNobody());
            yield return new Projection3(otherProjectionWriter);
        }

        /// <summary>
        /// White box testing, ugly but simple to understand how to access internal clients of the
        /// Engine
        /// </summary>
        [Test]
        public void verify_starting_position_of_pollers()
        {
            CommitPollingClient2[] clients = ((ICommitPollingClient[])Engine.GetFieldValue("_clients")).Cast<CommitPollingClient2>().ToArray();
            Assert.That(clients.Length, Is.EqualTo(2));

            // no projection dispatched anything, we just check if last dispatched is -1
            Assert.That(clients.All(c => c.LastDispatchedPosition == 0));
        }

        protected override List<BucketInfo> GetBucketInformations()
        {
            return new List<BucketInfo>() {
                new BucketInfo() { Slots = new[] { "*" }, BufferSize = 10 },
                new BucketInfo() { Slots = new[] { "OtherSlotName" }, BufferSize = 100 },
            };
        }
    }

    [TestFixture("2")]
    public class ProjectionEngineTestsPollerStartingCheckpointWithExistingDispatched : ProjectionEngineBasicTestBase
    {
        public ProjectionEngineTestsPollerStartingCheckpointWithExistingDispatched(String pollingClientVersion) : base(pollingClientVersion)
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
            var firstProjection = new Projection(writer);

            //We simulate that projection Projection in slot default was dispatched already at 42.
            var existingCheckpoint = new Checkpoint("Projection", 42, "signature");
            existingCheckpoint.Current = 42;
            base._checkpoints.Save(existingCheckpoint, existingCheckpoint.Id);

            yield return firstProjection;

            var otherProjectionWriter = new CollectionWrapper<SampleReadModel3, string>(StorageFactory, new NotifyToNobody());
            yield return new Projection3(otherProjectionWriter);
        }

        /// <summary>
        /// White box testing, ugly but simple to understand how to access internal clients of the
        /// Engine
        /// </summary>
        [Test]
        public void verify_starting_position_of_pollers()
        {
            CommitPollingClient2[] clients = ((ICommitPollingClient[])Engine.GetFieldValue("_clients")).Cast<CommitPollingClient2>().ToArray();
            Assert.That(clients.Length, Is.EqualTo(2));

            // no projection dispatched anything, we just check if last dispatched is -1
            Assert.That(clients.Any(c => c.LastDispatchedPosition == 0));
            Assert.That(clients.Any(c => c.LastDispatchedPosition == 42));
        }

        protected override List<BucketInfo> GetBucketInformations()
        {
            return new List<BucketInfo>() {
                new BucketInfo() { Slots = new[] { "*" }, BufferSize = 10 },
                new BucketInfo() { Slots = new[] { "OtherSlotName" }, BufferSize = 100 },
            };
        }
    }
}
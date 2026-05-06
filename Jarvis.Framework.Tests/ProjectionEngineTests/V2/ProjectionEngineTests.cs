using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Tests.EngineTests;
using MongoDB.Driver;
using NStore.Core.Persistence;
using NStore.Domain;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        public async Task run_poll_and_wait()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
            Thread.Sleep(50);
            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1, reader.AllSortedById.Count());
        }
    }

    [TestFixture("2")]
    public class ProjectionEngineInterruptedRebuildError : ProjectionEngineBasicTestBase
    {
        public ProjectionEngineInterruptedRebuildError(String pollingClientVersion) : base(pollingClientVersion)
        {
        }

        /// <summary>
        /// Verify second part of #11561
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task run_poll_and_wait()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
            Thread.Sleep(50);
            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1, reader.AllSortedById.Count());

            // now stop the engine
            Engine.Stop();

            // now simulate an interrupted rebuild
            var checkpoint = _checkpoints.AsQueryable().First();
            checkpoint.Current = null;
            _checkpoints.Save(checkpoint, checkpoint.Id);

            try
            {
                Engine.StartSync(100);
                Assert.Fail("Exception expected");
            }
            catch (AggregateException aex)
            {
                var ex = aex.Flatten();
                Assert.That(ex.InnerException, Is.InstanceOf<JarvisFrameworkEngineException>());
            }
        }
    }

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
        public async Task start_with_rebuild_then_stop_rebuild()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
            Thread.Sleep(50);
            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1, reader.AllSortedById.Count());
            var checkpoint = _checkpoints.FindOneById("Projection");
            Assert.That(checkpoint.Value, Is.EqualTo(1), "Checkpoint is written after rebuild.");
        }
    }

    /// <summary>
    /// Regression tests for the "Sequence broken" bug that occurred when a chunk dispatch
    /// failed and was retried by <c>CommitPollingClient2</c>. Before the fix,
    /// <c>lastCheckpointDispatched</c> was written before the actual dispatch work, so
    /// a retry of the same chunk would trigger the sequence guard and throw
    /// <see cref="JarvisFrameworkEngineException"/> instead of succeeding.
    /// </summary>
    [TestFixture("2")]
    public class ProjectionEngineRetryOnFailureTests : AbstractV2ProjectionEngineTests
    {
        private FailingOnFirstCallProjection _failingProjection;

        public ProjectionEngineRetryOnFailureTests(String pollingClientVersion) : base(pollingClientVersion) { }

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
            _failingProjection = new FailingOnFirstCallProjection(writer);
            yield return _failingProjection;
        }

        private IChunk BuildFakeChunk(long position, object payload)
        {
            var chunk = Substitute.For<IChunk>();
            chunk.Position.Returns(position);
            chunk.PartitionId.Returns("SampleAggregate_1");
            chunk.OperationId.Returns(Guid.NewGuid().ToString());
            chunk.Payload.Returns(payload);
            return chunk;
        }

        /// <summary>
        /// When a projection throws during dispatch, the chunk must be retryable —
        /// i.e. calling <c>DispatchCommitAsync</c> again with the same chunk must NOT
        /// raise "Sequence broken". This simulates what <c>CommitPollingClient2</c> does
        /// in its retry loop.
        /// </summary>
        [Test]
        public async Task retry_of_failed_dispatch_does_not_trigger_sequence_broken()
        {
            var evt = new SampleAggregateCreated();
            evt.SetPropertyValue(d => d.AggregateId, new SampleAggregateId(1));
            var changeset = new Changeset(1, new object[] { evt });
            var chunk = BuildFakeChunk(1L, changeset);

            // First dispatch: the projection throws — the chunk should propagate the error.
            _failingProjection.ShouldFail = true;
            var dispatchMethod = Engine.GetType()
                .GetMethod("DispatchCommitAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var firstCall = (Task)dispatchMethod.Invoke(Engine, new object[] { chunk, "default", 0L });
            Assert.ThrowsAsync<InvalidOperationException>(async () => await firstCall.ConfigureAwait(false),
                "Expected projection failure to propagate");

            // Second dispatch (retry): projection succeeds now — must NOT throw "Sequence broken".
            _failingProjection.ShouldFail = false;
            var secondCall = (Task)dispatchMethod.Invoke(Engine, new object[] { chunk, "default", 0L });
            Assert.DoesNotThrowAsync(async () => await secondCall.ConfigureAwait(false),
                "Retry of same chunk must not trigger 'Sequence broken'");

            // Verify the read model was actually written by the successful retry.
            var reader = new MongoReader<SampleReadModel, string>(Database);
            Assert.That(reader.AllSortedById.Count(), Is.EqualTo(1), "Read model must be populated after successful retry");
        }
    }
}

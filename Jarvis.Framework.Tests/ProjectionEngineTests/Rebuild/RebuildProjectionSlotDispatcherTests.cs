using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Rebuild;
using Jarvis.Framework.Tests.EngineTests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Jarvis.Framework.TestHelpers;
using Fasterflect;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Kernel.Support;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.Rebuild
{
    [TestFixture]
    public class RebuildProjectionSlotDispatcherTests
    {
        private RebuildProjectionSlotDispatcher sut;
        private IProjection[] projections;
        private const String slotName = "test";

        public void InitSut()
        {
            var config = new ProjectionEngineConfig();
            projections = new []{ new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>()) };
            sut = new RebuildProjectionSlotDispatcher(
                NullLogger.Instance,
                slotName,
                config,
                projections,
                4,
				NullLoggerThreadContextManager.Instance);

            //Needed to avoid crash on wrong metrics dispatch.
            KernelMetricsHelper.CreateMeterForRebuildDispatcherBuffer(slotName, () => 0);
        }

        [Test]
        public async Task verify_finished_with_marker_events()
        {
            InitSut();
            await DispatchEventAsync(1).ConfigureAwait(false);
            Assert.That(sut.Finished, Is.False);
            await DispatchEventAsync(2).ConfigureAwait(false);
            Assert.That(sut.Finished, Is.False);
            await DispatchEventAsync(3).ConfigureAwait(false);
            Assert.That(sut.Finished, Is.False);
            await DispatchEventAsync(4).ConfigureAwait(false);
            Assert.That(sut.Finished, Is.False);
			await sut.DispatchEventAsync(UnwindedDomainEvent.LastEvent).ConfigureAwait(false);
			Assert.That(sut.Finished, Is.True);
		}

        private async Task<SampleAggregateCreated> DispatchEventAsync(Int64 checkpointToken)
        {
            var evt = new SampleAggregateCreated()
            {
                MessageId = Guid.Parse("fc3e5f0a-c4f0-47d5-91cf-a1c87fee600f")
            };

            evt.AssignIdForTest(new SampleAggregateId(1));
            evt.SetPropertyValue(d => d.CheckpointToken, checkpointToken);

			UnwindedDomainEvent uevt = new UnwindedDomainEvent();
			uevt.PartitionId = evt.AggregateId;
			uevt.CheckpointToken = checkpointToken;
			uevt.Event = evt;
			uevt.EventType = evt.GetType().Name;

            await sut.DispatchEventAsync(uevt).ConfigureAwait(false);
            return evt;
        }
    }
}

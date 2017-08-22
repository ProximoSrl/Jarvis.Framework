using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.ProjectionEngine.Rebuild;
using Jarvis.Framework.Tests.EngineTests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.TestHelpers;
using Fasterflect;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.Rebuild
{
    [TestFixture]
    public class RebuildProjectionSlotDispatcherTests
    {
        RebuildProjectionSlotDispatcher sut;
        IProjection[] projections;

        public void InitSut()
        {
            var config = new ProjectionEngineConfig();
            projections = new []{ new Projection(Substitute.For<ICollectionWrapper<SampleReadModel, String>>()) };
            sut = new RebuildProjectionSlotDispatcher(
                NullLogger.Instance,
                "test",
                config,
                projections,
                Substitute.For<IConcurrentCheckpointTracker>(),
                4);
        }

        [Test]
        public async Task verify_finished_with_consecutive_events()
        {
            InitSut();
            await DispatchEventAsync(1);
            Assert.That(sut.Finished, Is.False);
            await DispatchEventAsync(2);
            Assert.That(sut.Finished, Is.False);
            await DispatchEventAsync(3);
            Assert.That(sut.Finished, Is.False);
            await DispatchEventAsync(4);
            Assert.That(sut.Finished, Is.True);
        }

        [Test]
        public async Task verify_finished_with_no_consecutive_events()
        {
            InitSut();
            await DispatchEventAsync(1);
            Assert.That(sut.Finished, Is.False);
            await DispatchEventAsync(4);
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
            await sut.DispatchEventAsync(evt).ConfigureAwait(false);
            return evt;
        }
    }
}

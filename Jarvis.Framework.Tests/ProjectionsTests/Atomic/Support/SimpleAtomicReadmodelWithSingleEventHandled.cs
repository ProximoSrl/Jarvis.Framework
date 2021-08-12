using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using System;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support
{
    [AtomicReadmodelInfo("SimpleAtomicReadmodelWithSingleEventHandled", typeof(SampleAggregateId))]
    public class SimpleAtomicReadmodelWithSingleEventHandled : AbstractAtomicReadModel
    {
        public SimpleAtomicReadmodelWithSingleEventHandled(string id) : base(id)
        {
        }

        public Int32 TouchCount { get; private set; }

#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable S1172 // Unused method parameters should be removed

        private void On(SampleAggregateTouched _)
        {
            TouchCount += 1;
        }
#pragma warning restore S1172 // Unused method parameters should be removed
#pragma warning restore S1144 // Unused private types or members should be removed

        protected override int GetVersion()
        {
            return 1;
        }
    }
}

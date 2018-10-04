using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support
{
    [AtomicReadmodelInfo("AnotherSimpleTestAtomicReadModel", typeof(SampleAggregateId))]
    public class AnotherSimpleTestAtomicReadModel : AbstractAtomicReadModel
    {
        public AnotherSimpleTestAtomicReadModel(string id) : base(id)
        {
        }

        public Int32 TouchCount { get; private set; }

        public Boolean Created { get; private set; }

#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable S1172 // Unused method parameters should be removed
        private void On(SampleAggregateCreated evt)
        {
            Created = true;
            TouchCount = 0;
        }

        private void On(SampleAggregateTouched evt)
        {
            if (TouchCount >= TouchMax)
                throw new Exception();

            TouchCount += FakeSignature;
        }
#pragma warning restore S1172 // Unused method parameters should be removed
#pragma warning restore S1144 // Unused private types or members should be removed

        protected override int GetVersion()
        {
            return FakeSignature;
        }

        public static Int32 FakeSignature { get; set; }

        public static Int32 TouchMax { get; set; } = Int32.MaxValue;
    }
}

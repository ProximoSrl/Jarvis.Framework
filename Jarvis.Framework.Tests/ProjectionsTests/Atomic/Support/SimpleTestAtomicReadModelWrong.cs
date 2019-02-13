using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using System;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support
{
    /// <summary>
    /// Wrong because it is using the very same name of another readmodel
    /// </summary>
    [AtomicReadmodelInfo("SimpleTestAtomicReadModel", typeof(SampleAggregateId))]
    public class SimpleTestAtomicReadModelWrong : AbstractAtomicReadModel
    {
        protected SimpleTestAtomicReadModelWrong(string id) : base(id)
        {
        }

        protected override int GetVersion()
        {
            throw new NotImplementedException();
        }
    }
}

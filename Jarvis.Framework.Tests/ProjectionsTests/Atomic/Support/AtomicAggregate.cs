using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel.Atomic;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support
{
    /// <summary>
    /// Another aggregate used to test atomic readmodels.
    /// </summary>
    public class AtomicAggregateId : EventStoreIdentity
    {
        public AtomicAggregateId(long id) : base(id)
        {
        }

        public AtomicAggregateId(string id) : base(id)
        {
        }
    }

    public class AtomicAggregate : AggregateRoot<AtomicAggregate.SampleAggregateState, AtomicAggregateId>
    {
        public class SampleAggregateState : JarvisAggregateState
        {
        }

        public void Create()
        {
            RaiseEvent(new AtomicAggregateCreated());
        }
    }

    public class AtomicAggregateCreated : DomainEvent
    {
    }

    [AtomicReadmodelInfo("SimpleAtomicAggregateReadModel", typeof(AtomicAggregateId))]
    public class SimpleAtomicAggregateReadModel : AbstractAtomicReadModel
    {
        public SimpleAtomicAggregateReadModel(string id) : base(id)
        {
        }

        public bool Created { get; private set; }

        protected override int GetVersion()
        {
            return 1;
        }

#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable RCS1163 // Unused parameter.
#pragma warning disable S1172 // Unused method parameters should be removed
        private void On(AtomicAggregateCreated evt)
        {
            Created = true;
        }
#pragma warning restore S1172 // Unused method parameters should be removed
#pragma warning restore RCS1163 // Unused parameter.
#pragma warning restore S1144 // Unused private types or members should be removed
    }
}

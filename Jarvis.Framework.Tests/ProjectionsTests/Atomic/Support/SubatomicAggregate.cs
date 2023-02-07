using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using NUnit.Framework;
using System.Collections.Generic;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support
{
    /// <summary>
    /// Another aggregate used to test atomic readmodels.
    /// </summary>
    public class SubAtomicAggregateId : EventStoreIdentity
    {
        public SubAtomicAggregateId(long id) : base(id)
        {
        }

        public SubAtomicAggregateId(string id) : base(id)
        {
        }
    }

    public class SubAtomicAggregate : AggregateRoot<SubAtomicAggregate.SubSampleAggregateState, SubAtomicAggregateId>
    {
        public class SubSampleAggregateState : JarvisAggregateState
        {
        }

        public void Create()
        {
            RaiseEvent(new AtomicAggregateCreated());
        }
    }

    public class SubAtomicAggregateCreated : DomainEvent
    {
    }

	public class SubAtomicAggregatePoked : DomainEvent
	{
        public SubAtomicAggregatePoked(string pokeReason)
        {
			PokeReason = pokeReason;
		}

		public string PokeReason { get; private set; }
	}

	[AtomicReadmodelInfo("SimpleAtomicAggregateReadModel", typeof(AtomicAggregateId))]
    public class SimpleSubAtomicAggregateReadModel : AbstractAtomicReadModel
    {
        public SimpleSubAtomicAggregateReadModel(string id) : base(id)
        {
        }

        public bool Created { get; private set; }

        public List<string> PokeReasons { get; private set; } = new List<string>();

        protected override int GetVersion()
        {
            return 1;
        }

#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable RCS1213 // Remove unused member declaration.
		private void On(SubAtomicAggregateCreated _)

		{
            Created = true;
        }

		private void On(SubAtomicAggregatePoked evt)
		{
            PokeReasons.Add(evt.PokeReason);
		}
#pragma warning restore RCS1213 // Remove unused member declaration.
#pragma warning restore IDE0051 // Remove unused private members
	}
}

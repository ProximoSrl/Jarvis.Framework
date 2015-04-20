using System;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.Tests.EngineTests
{
    public class SampleAggregateId : EventStoreIdentity
    {
	    public SampleAggregateId(long id) : base(id)
	    {
	    }

	    public SampleAggregateId(string id) : base(id)
	    {
	    }
    }

    public class SampleAggregate : AggregateRoot<SampleAggregate.State>
    {
        public class State : AggregateState
        {
            public Boolean ShouldBeFalse { get; private set; }

            protected void When(SampleAggregateCreated evt)
            {
            }

            protected void When(SampleAggregateInvalidated evt)
            {
                ShouldBeFalse = true;
            }

            public override InvariantCheckResult CheckInvariants()
            {
                if (ShouldBeFalse == true) return InvariantCheckResult.CreateForError("Invariant not satisfied");

                return InvariantCheckResult.Success;
            }
        }

        public SampleAggregate()
        {
        }

		public void Create()
		{
			RaiseEvent(new SampleAggregateCreated());
		}

        public void InvalidateState()
        {
            RaiseEvent(new SampleAggregateInvalidated());
        }

        public void Touch()
        {
            RaiseEvent(new SampleAggregateTouched());
        }
    }

    public class SampleAggregateForInspection : SampleAggregate
    {
        public IRouteEventsEx GetRouter()
        {
            return this.RegisteredRoutes;
        }
    }

    public class SampleAggregateCreated : DomainEvent
    {
        public SampleAggregateCreated()
        {
        }
    }

    public class SampleAggregateTouched : DomainEvent
    {
    }

    public class SampleAggregateInvalidated : DomainEvent
    {
        public SampleAggregateInvalidated()
        {
        }
    }
}
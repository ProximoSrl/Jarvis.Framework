using System;
using System.Collections.Generic;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.Commands;
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

            public Int32 TouchCount { get; set; }

            protected void When(SampleAggregateCreated evt)
            {
            }

            protected void When(SampleAggregateTouched evt)
            {
                TouchCount++;
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

            protected override object DeepCloneMe()
            {
                return new State() { TouchCount = this.TouchCount };
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

        public void DoubleTouch()
        {
            RaiseEvent(new SampleAggregateTouched());
            RaiseEvent(new SampleAggregateTouched());
        }

        public IDictionary<string, object> ExposeContext
        {
            get { return Context;}
        }

        public new State InternalState { get { return base.InternalState; } }
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

    public class SampleAggregateBaseEvent : DomainEvent { }

    public class SampleAggregateDerived1 : SampleAggregateBaseEvent { }

    public class SampleAggregateDerived2 : SampleAggregateBaseEvent { }

    public class SampleAggregateInvalidated : DomainEvent
    {
        public SampleAggregateInvalidated()
        {
        }
    }

    public class TouchSampleAggregate : Command<SampleAggregateId>
    {
        public TouchSampleAggregate(SampleAggregateId id)
            :base(id)
        {
        }
    }

    public class TouchSampleAggregateHandler : RepositoryCommandHandler<SampleAggregate, TouchSampleAggregate>
    {
        protected override void Execute(TouchSampleAggregate cmd)
        {
            FindAndModify(cmd.AggregateId,
                a =>
                {
                    if(!a.HasBeenCreated)
                        a.Create();
                    a.Touch();

                    Aggregate = a;
                }
                ,true);
        }

        public SampleAggregate Aggregate { get; private set; }
    }
}
using System;
using System.Collections.Generic;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using System.Threading.Tasks;
using NUnit.Framework;
using NStore.Domain;
using Jarvis.Framework.TestHelpers;
using Fasterflect;

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

	public class SampleAggregate : AggregateRoot<SampleAggregate.SampleAggregateState, SampleAggregateId>
	{
		public class SampleAggregateState : JarvisAggregateState
		{
			public Boolean ShouldBeFalse { get; private set; }

			public Int32 TouchCount { get; set; }

#pragma warning disable RCS1163 // Unused parameter.
			protected void When(SampleAggregateCreated evt)
			{
				// Method intentionally left empty.
			}

			protected void When(SampleAggregateTouched evt)
			{
				TouchCount++;
			}

			protected void When(SampleAggregateInvalidated evt)
			{
				ShouldBeFalse = true;
			}

#pragma warning restore RCS1163 // Unused parameter.

			protected override InvariantsCheckResult OnCheckInvariants()
			{
				return ShouldBeFalse ? InvariantsCheckResult.Invalid("Test error") : InvariantsCheckResult.Ok;
			}

			protected override object DeepCloneMe()
			{
				return new SampleAggregateState() { TouchCount = this.TouchCount };
			}
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

		public void TouchWithThrow()
		{
			RaiseEvent(new SampleAggregateTouched());
			throw new AssertionException("This method is supposed to throw exception");
		}

		public void DoubleTouch()
		{
			RaiseEvent(new SampleAggregateTouched());
			RaiseEvent(new SampleAggregateTouched());
		}

		public new SampleAggregateState InternalState { get { return base.InternalState; } }
	}

	public class SampleAggregateForInspection : SampleAggregate
	{
	}

	public class SampleAggregateCreated : DomainEvent
	{
    }

	public class SampleAggregateTouched : DomainEvent
	{
	}

	public class SampleAggregateBaseEvent : DomainEvent
	{
	}

	public class SampleAggregateDerived1 : SampleAggregateBaseEvent
	{
	}

	public class SampleAggregateDerived2 : SampleAggregateBaseEvent
	{
	}

	public class SampleAggregateInvalidated : DomainEvent
	{
		public SampleAggregateInvalidated()
		{
		}
	}

	public class TouchSampleAggregate : Command<SampleAggregateId>
	{
		public TouchSampleAggregate(SampleAggregateId id)
			: base(id)
		{
		}
	}

	public class TouchSampleAggregateHandler : RepositoryCommandHandler<SampleAggregate, TouchSampleAggregate>
	{
		protected override Task Execute(TouchSampleAggregate cmd)
		{
			return FindAndModifyAsync(cmd.AggregateId,
				a =>
				{
					if (!a.HasBeenCreated)
						a.Create();
					a.Touch();

					Aggregate = a;
				}
				, true);
		}

		public SampleAggregate Aggregate { get; private set; }
	}
}
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
using Jarvis.Framework.Shared.Store;

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

	[VersionInfo(Version = 1, Name = "SampleAggregateCreated")]
	public class SampleAggregateCreated : DomainEvent
	{
	}

	[VersionInfo(Version = 1, Name = "SampleAggregateUpcasted")]
	public class SampleAggregateUpcasted_v1 : DomainEvent
	{
        public string Value { get; set; }

		public class SampleAggregateUpcasted_v1Upcaster : BaseUpcaster<SampleAggregateUpcasted_v1, SampleAggregateUpcasted_v2>
		{
			protected override SampleAggregateUpcasted_v2 OnUpcast(SampleAggregateUpcasted_v1 eventToUpcast)
			{
				return new SampleAggregateUpcasted_v2()
				{
					Value = Int32.Parse(eventToUpcast.Value),
				};
			}
		}
	}

	[VersionInfo(Version = 2, Name = "SampleAggregateUpcasted")]
	public class SampleAggregateUpcasted_v2 : DomainEvent
	{
		public int Value { get; set; }

		public class SampleAggregateUpcasted_v2Upcaster : BaseUpcaster<SampleAggregateUpcasted_v2, SampleAggregateUpcasted>
		{
			protected override SampleAggregateUpcasted OnUpcast(SampleAggregateUpcasted_v2 eventToUpcast)
			{
				return new SampleAggregateUpcasted()
				{
					ValueTest = eventToUpcast.Value / 10,
				};
			}
		}
	}

	[VersionInfo(Version = 3, Name = "SampleAggregateUpcasted")]
	public class SampleAggregateUpcasted : DomainEvent
	{
        public Int32 ValueTest { get; set; }
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
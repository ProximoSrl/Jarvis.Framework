using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Tests.EngineTests.AggregateTests
{
    public class AggregateTestSampleAggregate1 :
		AggregateRoot<AggregateTestSampleAggregate1State, AggregateTestSampleAggregate1Id>
	{
		private AggregateTestSampleEntity sample;

		public AggregateTestSampleEntity SampleEntity => sample;

        protected override void AfterInit()
		{
			base.AfterInit();
		}

        protected override IEnumerable<IEntityRoot> CreateChildEntities(String aggregateId)
		{
			yield return sample = new AggregateTestSampleEntity($"{aggregateId}/childEntity");
		}

		public void Create()
		{
			RaiseEvent(new AggregateTestSampleAggregate1Created());
		}

		public void InvalidateState()
		{
			RaiseEvent(new AggregateTestSampleAggregate1Invalidated());
		}

		public void Touch()
		{
			RaiseEvent(new AggregateTestSampleAggregate1Touched());
		}

		public void TouchWithThrow()
		{
			RaiseEvent(new AggregateTestSampleAggregate1Touched());
			throw new AssertionException("This method is supposed to throw exception");
		}

		public void DoubleTouch()
		{
			RaiseEvent(new AggregateTestSampleAggregate1Touched());
			RaiseEvent(new AggregateTestSampleAggregate1Touched());
		}

		public new AggregateTestSampleAggregate1State InternalState { get { return base.InternalState; } }
	}

	public class AggregateTestSampleAggregate1Id : EventStoreIdentity
	{
		public AggregateTestSampleAggregate1Id(long id) : base(id)
		{
		}

		public AggregateTestSampleAggregate1Id(string id) : base(id)
		{
		}
	}

	public class AggregateTestSampleAggregate1State : JarvisAggregateState
	{
		public Boolean ShouldBeFalse { get; private set; }

		public Int32 TouchCount { get; set; }

		public void SetSignature(String signature)
		{
			VersionSignature = signature;
		}

#pragma warning disable RCS1163 // Unused parameter.

		protected void When(AggregateTestSampleAggregate1Created evt)
		{
			// Method intentionally left empty.
		}

		protected void When(AggregateTestSampleAggregate1Touched evt)
		{
			TouchCount++;
		}

		protected void When(AggregateTestSampleAggregate1Invalidated evt)
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
			return new AggregateTestSampleAggregate1State() { TouchCount = this.TouchCount };
		}
	}

	public class AggregateTestSampleAggregate1Created : DomainEvent
	{
	}

	public class AggregateTestSampleAggregate1Touched : DomainEvent
	{
	}

	public class AggregateTestSampleAggregate1Invalidated : DomainEvent
	{
	}

	#region entity

	public class AggregateTestSampleEntity : JarvisEntityRoot<AggregateTestSampleEntityState>
	{
		public AggregateTestSampleEntity(String id) : base(id)
		{
            AggregateEvents = new List<Object>();
        }

        public List<Object> AggregateEvents { get; set; }

        public AggregateTestSampleEntityState InternalState => base.InternalState;

        protected override void OnEventEmitting(object @event)
        {
            AggregateEvents.Add(@event);
            base.OnEventEmitting(@event);
        }

        public void AddValue(Int32 value)
		{
			if (base.InternalState.Accumulator > 100)
				ThrowDomainException("Cannot add when accumulator greater than 100");

			RaiseEvent(new AggregateTestSampleEntityAddValue(value));
		}
	}

	public class AggregateTestSampleEntityState : JarvisEntityState
	{
		public Int32 Accumulator { get; set; }

		public void SetSignature(String signature)
		{
			VersionSignature = signature;
		}

#pragma warning disable RCS1163 // Unused parameter.

		protected void When(AggregateTestSampleEntityAddValue evt)
		{
			Accumulator += evt.Value;
		}
#pragma warning restore RCS1163 // Unused parameter.

		protected override InvariantsCheckResult OnCheckInvariants()
		{
			return Accumulator == 42 ? InvariantsCheckResult.Invalid("Test error") : InvariantsCheckResult.Ok;
		}

		protected override object DeepCloneMe()
		{
			return new AggregateTestSampleEntityState() { Accumulator = this.Accumulator };
		}
	}

	public class AggregateTestSampleEntityAddValue : DomainEvent
	{
		public AggregateTestSampleEntityAddValue(int value)
		{
			Value = value;
		}

		public Int32 Value { get; set; }
	}
	#endregion
}

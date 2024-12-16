using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Shared.Store;
using System.Collections.Generic;

namespace Jarvis.Framework.Tests.EngineTests
{
	public class ComplexAggregateId : EventStoreIdentity
	{
		public ComplexAggregateId(long id) : base(id)
		{
		}

		public ComplexAggregateId(string id) : base(id)
		{
		}
	}

	public class ComplexAggregate : AggregateRoot<ComplexAggregate.ComplexAggregateState, ComplexAggregateId>
	{
		public class ComplexAggregateState : JarvisAggregateState
		{
            protected override object DeepCloneMe()
            {
                return MemberwiseClone();
            }
        }

		public void Create()
		{
			RaiseEvent(new ComplexAggregateBorn());
		}

		public void Done(string value)
		{
			RaiseEvent(new ComplexAggregateDone(value));
		}
	}

	[VersionInfo(Version = 1, Name = "ComplexAggregateBorn")]
	public class ComplexAggregateBorn : DomainEvent
	{
	}

	[VersionInfo(Version = 1, Name = "ComplexAggregateDone")]
	public class ComplexAggregateDone : DomainEvent
	{
		public ComplexAggregateDone(string value)
		{
			Value = value;
		}

		public string Value { get; set; }
	}

	public class ComplexAggregateReadModel : AbstractAtomicReadModel
	{
		public ComplexAggregateReadModel(string id) : base(id)
		{
		}

		protected override int GetVersion()
		{
			return 1;
		}

		public bool Born { get; set; }

		public List<string> DoneValues { get; set; } = new List<string>();

#pragma warning disable RCS1213 // Remove unused member declaration.
#pragma warning disable IDE0051 // Remove unused private members
		private void On(ComplexAggregateBorn _)
		{
			Born = true;
		}

		private void On(ComplexAggregateDone evt)
		{
			DoneValues.Add(evt.Value);
		}

#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore RCS1213 // Remove unused member declaration.
	}
}
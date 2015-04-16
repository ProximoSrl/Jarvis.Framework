using System;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.Events;
using Jarvis.NEventStoreEx.CommonDomainEx;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
namespace Jarvis.Framework.Tests.EngineTests
{
	[TestFixture]
	public class AggregateTests
	{
		[Test]
		public void aggregate_should_have_correct_event_router()
		{
			var aggregate = new SampleAggregateForInspection();
			var router = aggregate.GetRouter();
			Assert.IsTrue(router is AggregateRootEventRouter<SampleAggregate.State>);
		}

		[Test]
		public void hasBeenCreated_on_a_new_aggregate_should_return_false()
		{
			var aggregate = new SampleAggregate();
			Assert.IsFalse(aggregate.HasBeenCreated);
		}
	}
}

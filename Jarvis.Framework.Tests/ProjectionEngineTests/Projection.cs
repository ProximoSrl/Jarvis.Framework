using System;
using System.Threading;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.SharedTests;
using Jarvis.Framework.Tests.SharedTests.IdentitySupport;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
	[ProjectionInfo("Projection")]
	public class Projection : AbstractProjection,
		IEventHandler<SampleAggregateCreated>
	{
		readonly ICollectionWrapper<SampleReadModel, string> _collection;

		public Projection(ICollectionWrapper<SampleReadModel, string> collection)
		{
			_collection = collection;
			_collection.Attach(this, false);
		}

		public override Task DropAsync()
		{
			return _collection.DropAsync();
		}

		public override Task SetUpAsync()
		{
			return Task.CompletedTask;
		}		

		public Task On(SampleAggregateCreated e)
		{
			return _collection.InsertAsync(e, new SampleReadModel()
			{
				Id = e.AggregateId.AsString(),
				IsInRebuild = base.IsRebuilding,
				Timestamp = DateTime.Now.Ticks
			});
		}
	}

	[ProjectionInfo("ProjectionWithPoco")]
	public class ProjectionWithPoco : AbstractProjection,
		IEventHandler<SampleAggregateCreated>,
		IEventHandler<PocoPayloadObject>
	{
		readonly ICollectionWrapper<SampleReadModel, string> _collection;

		public ProjectionWithPoco(ICollectionWrapper<SampleReadModel, string> collection)
		{
			_collection = collection;
			_collection.Attach(this, false);
		}

		public override Task DropAsync()
		{
			return _collection.DropAsync();
		}

		public override Task SetUpAsync()
		{
			return Task.CompletedTask;
		}

		public Task On(SampleAggregateCreated e)
		{
			return _collection.InsertAsync(e, new SampleReadModel()
			{
				Id = e.AggregateId.AsString(),
				IsInRebuild = base.IsRebuilding,
				Timestamp = DateTime.Now.Ticks
			});
		}

		public Task On(PocoPayloadObject e)
		{
			return Task.CompletedTask;
		}
	}

	[ProjectionInfo("Projection2", Signature = "V2")]
	public class Projection2 : AbstractProjection,
	   IEventHandler<SampleAggregateCreated>
	{
		readonly ICollectionWrapper<SampleReadModel2, string> _collection;

		public Projection2(ICollectionWrapper<SampleReadModel2, string> collection)
		{
			_collection = collection;
			_collection.Attach(this, false);
		}

		public override int Priority
		{
			get { return 3; } //higher priority than previous projection
		}

		public override Task DropAsync()
		{
			return _collection.DropAsync();
		}

		public override Task SetUpAsync()
		{
			return Task.CompletedTask;
		}

		public async Task On(SampleAggregateCreated e)
		{
			await _collection.InsertAsync(e, new SampleReadModel2()
			{
				Id = e.AggregateId.AsString(),
				IsInRebuild = base.IsRebuilding,
				Timestamp = DateTime.Now.Ticks
			}).ConfigureAwait(false);
			Thread.Sleep(10);
		}
	}

	[ProjectionInfo("OtherSlotName", "v1", "Projection3")]
	public class Projection3 : AbstractProjection,
	 IEventHandler<SampleAggregateCreated>
	{
		readonly ICollectionWrapper<SampleReadModel3, string> _collection;

		public Projection3(ICollectionWrapper<SampleReadModel3, string> collection)
		{
			_collection = collection;
			_collection.Attach(this, false);
			Signature = base.Info.Signature;
		}

		public override int Priority
		{
			get { return 3; } //higher priority than previous projection
		}

		public override Task DropAsync()
		{
			return _collection.DropAsync();
		}

		public override Task SetUpAsync()
		{
			return Task.CompletedTask;
		}

		public String Signature
		{
			get { return _projectionInfoAttribute.Signature; }
			set { _projectionInfoAttribute = new ProjectionInfoAttribute(_projectionInfoAttribute.SlotName, value, _projectionInfoAttribute.CommonName); }
		}

		public async Task On(SampleAggregateCreated e)
		{
			Console.WriteLine("Projected in thread {0} - {1}",
				Thread.CurrentThread.ManagedThreadId,
				Thread.CurrentThread.Name);
			Thread.Sleep(0);
			await _collection.InsertAsync(e, new SampleReadModel3()
			{
				Id = e.AggregateId.AsString(),
				IsInRebuild = base.IsRebuilding,
				Timestamp = DateTime.Now.Ticks
			}).ConfigureAwait(false);
		}
	}

	[ProjectionInfo("ProjectionTypedId")]
	public class ProjectionTypedId : AbstractProjection
	{
		readonly ICollectionWrapper<SampleReadModelTest, TestId> _collection;

		public ProjectionTypedId(ICollectionWrapper<SampleReadModelTest, TestId> collection)
		{
			_collection = collection;
			_collection.Attach(this, false);
		}

		public override Task DropAsync()
		{
			return _collection.DropAsync();
		}

		public override Task SetUpAsync()
		{
			return Task.CompletedTask;
		}
	}

	[ProjectionInfo("ProjectionPollableReadmodel")]
	public class ProjectionPollableReadmodel : AbstractProjection
	{
		readonly ICollectionWrapper<SampleReadModelPollableTest, TestId> _collection;

		public ProjectionPollableReadmodel(ICollectionWrapper<SampleReadModelPollableTest, TestId> collection)
		{
			_collection = collection;
			_collection.Attach(this, false);
		}

		public override Task DropAsync()
		{
			return _collection.DropAsync();
		}

		public override Task SetUpAsync()
		{
			return Task.CompletedTask;
		}
	}

	public class PocoPayloadObject
	{
		public PocoPayloadObject(string propertyString, int propertyInt)
		{
			PropertyString = propertyString;
			PropertyInt = propertyInt;
		}

		public String PropertyString { get; set; }

		public Int32 PropertyInt { get; set; }
	}
}
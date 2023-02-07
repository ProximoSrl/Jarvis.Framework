using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support
{
	[AtomicReadmodelInfo("SimpleTestAtomicReadModel", typeof(SampleAggregateId))]
	public class SimpleTestAtomicReadModel : AbstractAtomicReadModel
	{
		public SimpleTestAtomicReadModel(string id) : base(id)
		{
		}

		public Int32 TouchCount { get; private set; }

		public Boolean Created { get; private set; }

		public String ExtraString { get; set; }

		protected override void BeforeEventProcessing(DomainEvent domainEvent)
		{
			ExtraString += $"B-{domainEvent.MessageId}";
			base.BeforeEventProcessing(domainEvent);
		}

		protected override void AfterEventProcessing(DomainEvent domainEvent)
		{
			ExtraString += $"A-{domainEvent.MessageId}";
			base.AfterEventProcessing(domainEvent);
		}

#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable RCS1213 // Remove unused member declaration.
		private void On(SampleAggregateCreated evt)
		{
			ExtraString += $"IN-{evt.MessageId}";
			Created = true;
			TouchCount = 0;
		}

		private void On(SampleAggregateTouched _)
		{
			if (TouchCount >= TouchMax)
			{
				if (GenerateInternalExceptionforMaxTouch)
				{
					throw new JarvisFrameworkEngineException("Internal exception for test");
				}
				else
				{
					throw new Exception("Exception for test");
				}
			}

			TouchCount += FakeSignature;
		}
#pragma warning restore RCS1213 // Remove unused member declaration.
#pragma warning restore IDE0051 // Remove unused private members

		protected override int GetVersion()
		{
			return FakeSignature;
		}

		public static Int32 FakeSignature { get; set; }

		public static Int32 TouchMax { get; set; } = Int32.MaxValue;
		public static bool GenerateInternalExceptionforMaxTouch { get; set; }
	}

	public class SimpleTestAtomicReadModelInitializer : IAtomicReadModelInitializer
	{
		private readonly IAtomicCollectionWrapper<SimpleTestAtomicReadModel> _atomicCollectionWrapper;

		public SimpleTestAtomicReadModelInitializer(IAtomicCollectionWrapper<SimpleTestAtomicReadModel> atomicCollectionWrapper)
		{
			_atomicCollectionWrapper = atomicCollectionWrapper;
		}

		public Boolean Initialized { get; set; }

		public Task Initialize()
		{
			Initialized = true;
			return Task.CompletedTask;
		}
	}
}

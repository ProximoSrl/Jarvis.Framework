using Castle.Windsor;
using Jarvis.Framework.Kernel.Engine;
using NStore.Core.InMemory;
using NStore.Core.Streams;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.EngineTests.AggregateTests
{
	[TestFixture]
	public class AggregateRepositoryTests
	{
		private Repository sut;
		private WindsorContainer container;

		[SetUp]
		public void SetUp()
		{
			container = new WindsorContainer();
			var memoryPersistence = new InMemoryPersistence();
			sut = new Repository(new AggregateFactoryEx(container.Kernel), new StreamsFactory(memoryPersistence));
		}

		[TearDown]
		public void TearDown()
		{
			container.Dispose();
		}

		[Test]
		public async Task Verify_repository_restore_inner_entities()
		{
			var aggregate = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
			aggregate.Touch();
			aggregate.SampleEntity.AddValue(10);
			await sut.SaveAsync(aggregate, Guid.NewGuid().ToString()).ConfigureAwait(false);

			var reloaded = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
			Assert.That(reloaded.SampleEntity.InternalState.Accumulator, Is.EqualTo(10));
		}

		[Test]
		public async Task Verify_aggregate_State_invariants_check_invariants_of_entity_states()
		{
			var aggregate = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
			aggregate.Touch();
			aggregate.SampleEntity.AddValue(42);
			var entityInvariants = aggregate.SampleEntity.InternalState.CheckInvariants();
			Assert.That(entityInvariants.IsInvalid);
			var invariants = aggregate.InternalState.CheckInvariants();
			Assert.That(invariants.IsInvalid);
		}

		[Test]
		public async Task Verify_repository_save_checks_entity_state_invariant()
		{
			var aggregate = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
			aggregate.Touch();
			aggregate.SampleEntity.AddValue(42);
			Assert.ThrowsAsync<InvariantCheckFailedException>(() =>
				sut.SaveAsync(aggregate, Guid.NewGuid().ToString()));
		}

		[Test]
		public async Task Verify_extension_to_load_with_type()
		{
			var aggregateGeneric = await sut.GetByIdAsync<AggregateTestSampleAggregate1>("AggregateTestSampleAggregate1_42").ConfigureAwait(false);
			Assert.That(aggregateGeneric, Is.InstanceOf<AggregateTestSampleAggregate1>());

			var aggregate = await sut.GetByIdAsync(typeof(AggregateTestSampleAggregate1), "AggregateTestSampleAggregate1_42").ConfigureAwait(false);
			Assert.That(aggregate, Is.InstanceOf<AggregateTestSampleAggregate1>());
			Assert.That(aggregate.Id, Is.EqualTo("AggregateTestSampleAggregate1_42"));
		}

		[Test]
		public async Task Verify_extension_to_Save_with_type()
		{
			Type type = typeof(AggregateTestSampleAggregate1);
			var aggregate = (AggregateTestSampleAggregate1)await sut.GetByIdAsync(type, "AggregateTestSampleAggregate1_42").ConfigureAwait(false);
			aggregate.Touch();
			await sut.SaveAsync(aggregate, Guid.NewGuid().ToString(), accessor => accessor.Add("HEADER", "VALUE")).ConfigureAwait(false);

			var reloaded = (AggregateTestSampleAggregate1)await sut.GetByIdAsync(type, "AggregateTestSampleAggregate1_42").ConfigureAwait(false);
			Assert.That(reloaded.InternalState.TouchCount, Is.EqualTo(1));
		}
	}
}

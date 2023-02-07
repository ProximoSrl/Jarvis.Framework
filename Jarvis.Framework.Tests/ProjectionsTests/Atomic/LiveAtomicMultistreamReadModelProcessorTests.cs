using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
	[TestFixture]
	public class LiveAtomicMultistreamReadModelProcessorTests : AtomicProjectionEngineTestBase
	{
		private Changeset c1, c2, c3, c4, c5, c6, c7;
		private DateTime sequence1, sequence2, sequence3;

		[Test]
		public async Task Project_up_until_certain_checkpoint_all_projection()
		{
			await CreateScenario().ConfigureAwait(false);

			//ok now we can start to test
			var sut = _container.Resolve<ILiveAtomicMultistreamReadModelProcessor>();

			//ok now we want to project multiple stuff
			List<MultiStreamProcessRequest> request = new List<MultiStreamProcessRequest>
			{
				new MultiStreamProcessRequest(c1.GetIdentity().AsString(), new Type[] { typeof(SimpleTestAtomicReadModel) }),
				new MultiStreamProcessRequest(c4.GetIdentity().AsString(), new Type[] { typeof(ComplexAggregateReadModel) })
			};

			//project everything up to the most up to date stuff
			var result = await sut.ProcessAsync(request, c7.GetChunkPosition());
			var rms = result.Get<SimpleTestAtomicReadModel>(c1.GetIdentity().AsString());
			Assert.That(rms.TouchCount, Is.EqualTo(4));
			Assert.That(rms.AggregateVersion, Is.EqualTo(c7.AggregateVersion));

			var cms = result.Get<ComplexAggregateReadModel>(c4.GetIdentity().AsString());
			Assert.That(cms.Born, Is.True);
			Assert.That(cms.DoneValues, Is.EquivalentTo(new[] { "done1", "done2" }));
			Assert.That(cms.AggregateVersion, Is.EqualTo(c6.AggregateVersion));
		}

		[Test]
		public async Task Get_wrong_readmodel_return_null()
		{
			await CreateScenario().ConfigureAwait(false);

			//ok now we can start to test
			var sut = _container.Resolve<ILiveAtomicMultistreamReadModelProcessor>();

			//ok now we want to project multiple stuff
			List<MultiStreamProcessRequest> request = new List<MultiStreamProcessRequest>
			{
				new MultiStreamProcessRequest(c1.GetIdentity().AsString(), new Type[] { typeof(SimpleTestAtomicReadModel) }),
				new MultiStreamProcessRequest(c4.GetIdentity().AsString(), new Type[] { typeof(ComplexAggregateReadModel) })
			};

			//project everything up to the most up to date stuff
			var result = await sut.ProcessAsync(request, c7.GetChunkPosition());

			var rms = result.Get<SimpleTestAtomicReadModel>(c4.GetIdentity().AsString());
			Assert.That(rms, Is.Null);
		}

		[Test]
		public async Task Argument_check_for_nullability_chunk_position()
		{
			await CreateScenario().ConfigureAwait(false);

			//ok now we can start to test
			var sut = _container.Resolve<ILiveAtomicMultistreamReadModelProcessor>();
			Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.ProcessAsync(null, c7.GetChunkPosition()));

			//empty does not throw
			Assert.DoesNotThrowAsync(async () => await sut.ProcessAsync(new List<MultiStreamProcessRequest>(), c7.GetChunkPosition()));
		}

		[Test]
		public async Task Argument_check_for_nullability_datetime()
		{
			await CreateScenario().ConfigureAwait(false);

			//ok now we can start to test
			var sut = _container.Resolve<ILiveAtomicMultistreamReadModelProcessor>();
			Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.ProcessAsync(null, DateTime.UtcNow));

			//empty does not throw
			Assert.DoesNotThrowAsync(async () => await sut.ProcessAsync(new List<MultiStreamProcessRequest>(), DateTime.UtcNow));
		}

		[Test]
		public async Task Project_up_until_certain_checkpoint_number()
		{
			await CreateScenario().ConfigureAwait(false);

			//ok now we can start to test
			var sut = _container.Resolve<ILiveAtomicMultistreamReadModelProcessor>();

			//ok now we want to project multiple stuff
			List<MultiStreamProcessRequest> request = new List<MultiStreamProcessRequest>
			{
				new MultiStreamProcessRequest(c1.GetIdentity().AsString(), new Type[] { typeof(SimpleTestAtomicReadModel) }),
				new MultiStreamProcessRequest(c4.GetIdentity().AsString(), new Type[] { typeof(ComplexAggregateReadModel) })
			};

			//in c3 first aggregate exists, second aggregate no
			var result = await sut.ProcessAsync(request, c3.GetChunkPosition());
			var rms = result.Get<SimpleTestAtomicReadModel>(c1.GetIdentity().AsString());
			Assert.That(rms.TouchCount, Is.EqualTo(3));
			Assert.That(rms.AggregateVersion, Is.EqualTo(c2.AggregateVersion));

			var cms = result.Get<ComplexAggregateReadModel>(c4.GetIdentity().AsString());
			Assert.IsNull(cms);

			var other = result.Get<ComplexAggregateReadModel>(c3.GetIdentity().AsString());
			Assert.IsNull(other);
		}

		[Test]
		public async Task Project_up_until_certain_time_all_projection()
		{
			await CreateScenario().ConfigureAwait(false);

			//ok now we can start to test
			var sut = _container.Resolve<ILiveAtomicMultistreamReadModelProcessor>();

			//ok now we want to project multiple stuff
			List<MultiStreamProcessRequest> request = new List<MultiStreamProcessRequest>
			{
				new MultiStreamProcessRequest(c1.GetIdentity().AsString(), new Type[] { typeof(SimpleTestAtomicReadModel) }),
				new MultiStreamProcessRequest(c4.GetIdentity().AsString(), new Type[] { typeof(ComplexAggregateReadModel) })
			};

			//project everything up to the most up to date stuff
			var result = await sut.ProcessAsync(request, sequence2);

			var rms = result.Get<SimpleTestAtomicReadModel>(c1.GetIdentity().AsString());
			Assert.That(rms.TouchCount, Is.EqualTo(3));
			Assert.That(rms.AggregateVersion, Is.EqualTo(c2.AggregateVersion));

			var cms = result.Get<ComplexAggregateReadModel>(c4.GetIdentity().AsString());
			Assert.That(cms.Born, Is.True);
			Assert.That(cms.DoneValues, Is.EquivalentTo(new[] { "done1" }));
			Assert.That(cms.AggregateVersion, Is.EqualTo(c5.AggregateVersion));
		}

		private async Task CreateScenario()
		{
			sequence1 = new DateTime(2010, 10, 10);
			sequence2 = sequence1.AddDays(1);
			sequence3 = sequence1.AddDays(3);

			c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset(sequence1).ConfigureAwait(false);
			c2 = await GenerateTouchedEvent(timestamp: sequence2).ConfigureAwait(false);
			_aggregateIdSeed++;
			c3 = await GenerateSomeChangesetsAndReturnLatestsChangeset(sequence2).ConfigureAwait(false);
			_aggregateIdSeed++;
			//a complete different aggregate
			c4 = await GenerateBornEvent(sequence2);
			c5 = await GenerateComplexDoneEvent("done1", timeStamp: sequence2);
			c6 = await GenerateComplexDoneEvent("done2", timeStamp: sequence3);

			//now again the first aggregate
			var savedAggregateId = _aggregateIdSeed;
			_aggregateIdSeed = ((EventStoreIdentity)c1.GetIdentity()).Id;
			c7 = await GenerateTouchedEvent().ConfigureAwait(false);
			_aggregateIdSeed = savedAggregateId;
		}

		protected Task<Changeset> GenerateBornEvent(DateTime? timeStamp = null)
		{
			var evt = new ComplexAggregateBorn();
			SetBasePropertiesToEvent(evt, null, timestamp: timeStamp);
			return ProcessEvent(evt, id => new ComplexAggregateId(id));
		}

		protected Task<Changeset> GenerateComplexDoneEvent(String value, DateTime? timeStamp = null)
		{
			var evt = new ComplexAggregateDone(value);
			SetBasePropertiesToEvent(evt, null, timestamp: timeStamp);
			return ProcessEvent(evt, id => new ComplexAggregateId(id));
		}
	}
}

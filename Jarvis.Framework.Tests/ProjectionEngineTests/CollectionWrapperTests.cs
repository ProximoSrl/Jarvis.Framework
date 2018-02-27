using Fasterflect;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.SharedTests.IdentitySupport;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
	[TestFixture]
	public class CollectionWrapperTests
	{
		private CollectionWrapper<SampleReadModelTest, String> sut;

		private IMongoDatabase _db;
		private MongoClient _client;

		[OneTimeSetUp]
		public void TestFixtureSetUp()
		{
			var connectionString = ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString;
			var url = new MongoUrl(connectionString);
			_client = new MongoClient(url);
			_db = _client.GetDatabase(url.DatabaseName);

			TestHelper.RegisterSerializerForFlatId<TestId>();
		}

		[SetUp]
		public void SetUp()
		{
			_client.DropDatabase(_db.DatabaseNamespace.DatabaseName);
			var rebuildContext = new RebuildContext(false);
			var storageFactory = new MongoStorageFactory(_db, rebuildContext);
			sut = new CollectionWrapper<SampleReadModelTest, String>(storageFactory, new NotifyToNobody());
			//It is important to create the projection to attach the collection wrapper
			new ProjectionTypedId(sut);
		}

		[Test]
		public async Task Verify_basic_delete()
		{
			var rm = new SampleReadModelTest();
			rm.Id = new TestId(1);
			rm.Value = "test";
			await sut.InsertAsync(new SampleAggregateCreated(), rm).ConfigureAwait(false);
			var all = sut.All.ToList();
			Assert.That(all, Has.Count.EqualTo(1));

			await sut.DeleteAsync(new SampleAggregateInvalidated(), rm.Id).ConfigureAwait(false);
			all = sut.All.ToList();
			Assert.That(all, Has.Count.EqualTo(0));
		}

		[Test]
		public async Task Verify_check_on_creation_by_two_different_event()
		{
			var rm = new SampleReadModelTest
			{
				Id = new TestId(1),
				Value = "test"
			};
			SampleAggregateCreated e1 = new SampleAggregateCreated();
			await sut.InsertAsync(e1, rm).ConfigureAwait(false);

			SampleAggregateCreated e2 = new SampleAggregateCreated();
			//check we are not able to create a readmodel with two different source events.
			Assert.ThrowsAsync<CollectionWrapperException>(() => sut.InsertAsync(e2, rm));
		}

		

		[Test]
		public async Task Verify_basic_update()
		{
			var rm = new SampleReadModelTest();
			rm.Id = new TestId(1);
			rm.Value = "test";
			await sut.InsertAsync(new SampleAggregateCreated(), rm).ConfigureAwait(false);
			rm.Value = "test2";
			await sut.SaveAsync(new SampleAggregateTouched(), rm).ConfigureAwait(false);
			var all = sut.All.ToList();
			Assert.That(all, Has.Count.EqualTo(1));
			var loaded = all[0];
			Assert.That(loaded.Value, Is.EqualTo("test2"));
		}

		[Test]
		public async Task Verify_update_idempotency()
		{
			var rm = new SampleReadModelTest
			{
				Id = new TestId(1),
				Value = "test",
				Counter = 10,
			};
			await sut.InsertAsync(new SampleAggregateCreated(), rm).ConfigureAwait(false);

			//now try to update counter with an event
			SampleAggregateTouched e = new SampleAggregateTouched();

			await sut.FindAndModifyAsync(e, rm.Id, _ => _.Counter++).ConfigureAwait(false);
			var reloaded = await sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
			Assert.That(reloaded.Counter, Is.EqualTo(11));

			//idempotency on the very same event
			await sut.FindAndModifyAsync(e, rm.Id, _ => _.Counter++).ConfigureAwait(false);
			reloaded = await sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
			Assert.That(reloaded.Counter, Is.EqualTo(11));

			//increment on different event
			SampleAggregateTouched anotherEvent = new SampleAggregateTouched();
			await sut.FindAndModifyAsync(anotherEvent, rm.Id, _ => _.Counter++).ConfigureAwait(false);
			reloaded = await sut.FindOneByIdAsync(rm.Id).ConfigureAwait(false);
			Assert.That(reloaded.Counter, Is.EqualTo(12));
		}
	}
}

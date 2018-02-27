using System.Configuration;
using System.Linq;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.TestHelpers;
using MongoDB.Driver;
using NUnit.Framework;
using Jarvis.Framework.Shared.Helpers;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    [TestFixture]
    public class ProjectionTests
    {
        private MyProjection _projection;
        private CollectionWrapper<MyReadModel, string> _collection;
        private SpyNotifier _spyNotifier;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(url);
            var db = client.GetDatabase(url.DatabaseName);
            db.Drop();

            _spyNotifier = new SpyNotifier();
            _collection = new CollectionWrapper<MyReadModel, string>(new MongoStorageFactory(db, new RebuildContext(false)), _spyNotifier);
            _projection = new MyProjection(_collection);
            _collection.Attach(_projection);
        }

        [SetUp]
        public void SetUp()
        {
            _spyNotifier.Counter = 0;
            _projection.DropAsync().Wait();
            _projection.SetUpAsync().Wait();
        }

        [Test]
        public async Task Insert_is_idempotent()
        {
            var evt = new InsertEvent(1, 1, 1) { Text = "one" };
            await _projection.On(evt).ConfigureAwait(false);
            evt.Text = "two";
            await _projection.On(evt).ConfigureAwait(false);

            var loaded = _collection.Where(x => x.Id == evt.AggregateId.AsString()).Single();

            Assert.AreEqual(1001, loaded.LastEventIndexProjected);
            Assert.AreEqual("one", loaded.Text);
            Assert.AreEqual(1, _spyNotifier.Counter);
        }

        [Test]
        public async Task Save_honors_event_index()
        {
            var insert = new InsertEvent(1, 1, 1) { Text = "one" };
            var update1 = new UpdateEvent(2, 2, 1) { Text = "update 1" };
            var update2 = new UpdateEvent(2, 2, 2) { Text = "update 2" };

            await _projection.On(insert).ConfigureAwait(false);
            await _projection.On(update1).ConfigureAwait(false);
            await _projection.On(update2).ConfigureAwait(false);

            update1.Text = "skipped update 1";
            await _projection.On(update1).ConfigureAwait(false);

            var loaded = _collection.Where(x => x.Id == insert.AggregateId.AsString()).Single();

            Assert.AreEqual(2002, loaded.LastEventIndexProjected);
            Assert.AreEqual("update 2", loaded.Text);
            Assert.AreEqual(3, _spyNotifier.Counter);

            //re-test with the second event, we need to be pretty sure that everyting works with both the events
            update2.Text = "skipped update 2";
            await _projection.On(update1).ConfigureAwait(false);

            loaded = _collection.Where(x => x.Id == insert.AggregateId.AsString()).Single();

            Assert.AreEqual(2002, loaded.LastEventIndexProjected);
            Assert.AreEqual("update 2", loaded.Text);
            Assert.AreEqual(3, _spyNotifier.Counter);
        }

        [Test]
        public async Task Delete_is_idempotent()
        {
            var insert = new InsertEvent(1, 1, 1) { Text = "one" };
            var delete = new DeleteEvent(2, 2, 1);

            await _projection.On(insert).ConfigureAwait(false);
            await _projection.On(delete).ConfigureAwait(false);
            await _projection.On(delete).ConfigureAwait(false);

            var loaded = await _collection.FindOneByIdAsync(delete.AggregateId.AsString()).ConfigureAwait(false);

            Assert.IsNull(loaded);
        }

        [Test]
        public async Task Delete_does_not_generates_multiple_notifications()
        {
            var insert = new InsertEvent(1, 1, 1) { Text = "one" };
            var delete = new DeleteEvent(2, 2, 1);

            await _projection.On(insert).ConfigureAwait(false);
            await _projection.On(delete).ConfigureAwait(false);
            await _projection.On(delete).ConfigureAwait(false);

            Assert.AreEqual(2, _spyNotifier.Counter);
        }

        [Test]
        public async Task Upsert_create_new_readmodel()
        {
            var insert = new InsertEvent(1, 1, 1) { Text = "one" };

            await _collection.UpsertAsync(
                insert,
                insert.AggregateId.AsString(),
                () => new MyReadModel()
                {
                    Id = insert.AggregateId.AsString(),
                    Text = "created"
                },
                r => r.Text = "updated"
            ).ConfigureAwait(false);

            var saved = _collection.All.FirstOrDefault(x => x.Id == insert.AggregateId.AsString());

            Assert.IsNotNull(saved);
            Assert.AreEqual("created", saved.Text);
        }

        [Test]
        public async Task Upsert_update_old_readmodel()
        {
            var insert = new InsertEvent(1, 1, 1) { Text = "one" };
            var update = new InsertEvent(2, 1, 1) { Text = "one" };
            update.AssignIdForTest(insert.AggregateId);

           await _collection.InsertAsync(insert, new MyReadModel()
            {
                Id = insert.AggregateId.AsString(),
                Text = "created"
            }).ConfigureAwait(false);

            await _collection.UpsertAsync(
                update,
                update.AggregateId.AsString(),
                () => new MyReadModel()
                {
                    Id = insert.AggregateId.AsString(),
                    Text = "created"
                },
                r => r.Text = "updated"
            ).ConfigureAwait(false);

            var saved = _collection.All.FirstOrDefault(x => x.Id == insert.AggregateId.AsString());

            Assert.IsNotNull(saved);
            Assert.AreEqual("updated", saved.Text);
        }

        [Test]
        public async Task Collection_has_index()
        {
            Assert.IsTrue
            (
                await _collection.IndexExistsAsync(MyProjection.IndexName).ConfigureAwait(false)
            );
        }
    }
}

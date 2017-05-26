using System.Configuration;
using System.Linq;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.TestHelpers;
using MongoDB.Driver;
using NUnit.Framework;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    [TestFixture]
    public class ProjectionTests
    {
        MyProjection _projection;
        CollectionWrapper<MyReadModel, string> _collection;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(url);
            var db = client.GetDatabase(url.DatabaseName);
            db.Drop();

            _collection = new CollectionWrapper<MyReadModel, string>(new MongoStorageFactory(db, new RebuildContext(false)), new SpyNotifier());
            _projection = new MyProjection(_collection);
            _collection.Attach(_projection);
        }

        [SetUp]
        public void SetUp()
        {
            SpyNotifier.Counter = 0;
            _projection.Drop();
            _projection.SetUp();
        }

        [Test]
        public void insert_is_idempotent()
        {
            var evt = new InsertEvent() { Text = "one" };
            _projection.On(evt);
            evt.Text = "two";
            _projection.On(evt);

            var loaded = _collection.Where(x => x.Id == evt.AggregateId).Single();

            Assert.AreEqual(1, loaded.ProcessedEvents.Count);
            Assert.IsTrue(loaded.ProcessedEvents.Contains(evt.MessageId));
            Assert.AreEqual("one", loaded.Text);
            Assert.AreEqual(1, SpyNotifier.Counter);
        }

        [Test]
        public void save_is_idempotent()
        {
            var insert = new InsertEvent() { Text = "one" };
            var update = new UpdateEvent() { Text = "update" };

            _projection.On(insert);
            _projection.On(update);

            update.Text = "skipped update";
            _projection.On(update);

            var loaded = _collection.Where(x => x.Id == update.AggregateId).Single();

            Assert.AreEqual(2, loaded.ProcessedEvents.Count);
            Assert.IsTrue(loaded.ProcessedEvents.Contains(insert.MessageId));
            Assert.IsTrue(loaded.ProcessedEvents.Contains(update.MessageId));
            Assert.AreEqual("update", loaded.Text);
            Assert.AreEqual(2, SpyNotifier.Counter);
        }

        [Test]
        public void delete_is_idempotent()
        {
            var insert = new InsertEvent() { Text = "one" };
            var delete = new DeleteEvent();

            _projection.On(insert);
            _projection.On(delete);
            _projection.On(delete);

            var loaded = _collection.FindOneById(delete.AggregateId);

            Assert.IsNull(loaded);
        }

        [Test]
        public void delete_does_not_generates_multiple_notifications()
        {
            var insert = new InsertEvent() { Text = "one" };
            var delete = new DeleteEvent();

            _projection.On(insert);
            _projection.On(delete);
            _projection.On(delete);

            var loaded = _collection.FindOneById(delete.AggregateId);

            Assert.AreEqual(2, SpyNotifier.Counter);
        }

        [Test]
        public void upsert_create_new_readmodel()
        {
            var insert = new InsertEvent() { Text = "one" };

            _collection.Upsert(
                insert,
                insert.AggregateId,
                () => new MyReadModel()
                    {
                        Id = insert.AggregateId,
                        Text = "created"
                    },
                r => { r.Text = "updated"; }
            );

            var saved = _collection.All.FirstOrDefault(x => x.Id == insert.AggregateId);

            Assert.IsNotNull(saved);
            Assert.AreEqual("created", saved.Text);
        }

        [Test]
        public void upsert_update_old_readmodel()
        {
            var insert = new InsertEvent() { Text = "one" };
            var update = new InsertEvent() { Text = "one" };
            update.AssignIdForTest(insert.AggregateId);

            _collection.Insert(insert, new MyReadModel()
            {
                Id = insert.AggregateId,
                Text = "created"
            });

            _collection.Upsert(
                update,
                update.AggregateId,
                () => new MyReadModel()
                {
                    Id = insert.AggregateId,
                    Text = "created"
                },
                r => { r.Text = "updated"; }
            );

            var saved = _collection.All.FirstOrDefault(x => x.Id == insert.AggregateId);

            Assert.IsNotNull(saved);
            Assert.AreEqual("updated", saved.Text);
        }

        [Test]
        public void collection_has_index()
        {
            Assert.IsTrue
                (
                    _collection.IndexExists(MyProjection.IndexName)
                );
        }
    }
}

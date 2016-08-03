using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Tests.EngineTests.Unfolder;
using Jarvis.NEventStoreEx.CommonDomainEx;
using MongoDB.Driver;
using NEventStore;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.EngineTests
{
    [TestFixture]
    public class SnapshotPersistenceTest
    {
        MongoSnapshotPersisterProvider sut;

        private IMongoDatabase _db;
        private MongoClient _client;
        private MongoUrl _url;

        private const String _type = "type";
        private const String _bucket = "type";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
            _url = new MongoUrl(connectionString);
            _client = new MongoClient(_url);
            _db = _client.GetDatabase(_url.DatabaseName);
           
        }

        [SetUp]
        public void SetUp()
        {
            _client.DropDatabase(_url.DatabaseName);
            sut = new MongoSnapshotPersisterProvider(_db);
        }


        [Test]
        public void Smoke_persistence_of_snapshot()
        {
            Snapshot s = new Snapshot("test1", 1, "PAYLOAD");
            sut.Persist(s, _type);
        }

        [Test]
        public void Smoke_persistence_of_memento_snapshot()
        {
            TestProjector unfolder = new TestProjector();
            unfolder.ApplyEvent(new TestDomainEvent() { Value = 42 });
            var memento = unfolder.GetSnapshot();
            Snapshot s = new Snapshot("test1_a", 1, memento);
            sut.Persist(s, _type);
        }

        [Test]
        public void Basic_persistence_of_snapshot()
        {
            const string streamId = "test2";
            Snapshot s = new Snapshot(_bucket, streamId, 1, "PAYLOAD");
            sut.Persist(s, _type);

            var reloaded = sut.Load(streamId, 1, _type);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.BucketId, Is.EqualTo(_bucket));
            Assert.That(reloaded.StreamId, Is.EqualTo(streamId));
            Assert.That(reloaded.StreamRevision, Is.EqualTo(1));
            Assert.That(reloaded.Payload, Is.EqualTo("PAYLOAD"));
        }

        [Test]
        public void Basic_persistence_of_snapshot_memento()
        {
            const string streamId = "test2_a";
            TestProjector unfolder = new TestProjector();
            unfolder.ApplyEvent(new TestDomainEvent() { Value = 42 });
            var memento = unfolder.GetSnapshot();
            Snapshot s = new Snapshot(_bucket, streamId, 1, memento);
            sut.Persist(s, _type);

            var reloaded = sut.Load(streamId, 1, _type);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.BucketId, Is.EqualTo(_bucket));
            Assert.That(reloaded.StreamId, Is.EqualTo(streamId));
            Assert.That(reloaded.StreamRevision, Is.EqualTo(1));

            TestProjector unfolderReloaded = new TestProjector();
            unfolderReloaded.Restore((IMementoEx)reloaded.Payload);
            var readmodel = unfolderReloaded.GetProjection();
            Assert.That(readmodel.Sum, Is.EqualTo(42));
        }

        [Test]
        public void load_when_no_snapshot()
        {
            const string streamId = "test3";
             var reloaded = sut.Load(streamId, 1, _type);
            Assert.That(reloaded, Is.Null);
        }

        [Test]
        public void Load_most_recent_snapshot()
        {
            const string streamId = "test4";
            Snapshot s = new Snapshot(_bucket, streamId, 2, "PAYLOAD1");
            sut.Persist(s, _type);

            s = new Snapshot(_bucket, streamId, 5, "PAYLOAD2");
            sut.Persist(s, _type);

            var reloaded = sut.Load(streamId, 1, _type);
            Assert.That(reloaded, Is.Null, "no snapshot before version 1");

            reloaded = sut.Load(streamId, 2, _type);
            Assert.That(reloaded, Is.Not.Null, "Missing load of snapshot 2");
            Assert.That(reloaded.StreamRevision, Is.EqualTo(2));
            Assert.That(reloaded.Payload, Is.EqualTo("PAYLOAD1"));

            reloaded = sut.Load(streamId, 200, _type);
            Assert.That(reloaded, Is.Not.Null, "Missing load of snapshot 5");
            Assert.That(reloaded.StreamRevision, Is.EqualTo(5));
            Assert.That(reloaded.Payload, Is.EqualTo("PAYLOAD2"));
        }
    }
}

using Jarvis.Framework.Shared.Commands.Tracking;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Support;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.Support;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class MessageTrackerQueryManagerTest
    {
        private IMongoCollection<TrackedMessageModel> _messages;
        private MongoDbMessagesTracker _tracker;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            String connectionString = ConfigurationManager.ConnectionStrings["log"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = url.CreateClient(false);
            var db = client.GetDatabase(url.DatabaseName);
            db.Drop();
            _messages = db.GetCollection<TrackedMessageModel>("messages");
            _tracker = new MongoDbMessagesTracker(db);
            TestHelper.RegisterSerializerForFlatId<SampleAggregateId>();

            GenerateData();
        }

        private readonly SampleTestCommand message1 = new SampleTestCommand(10);
        private readonly SampleTestCommand message2 = new SampleTestCommand(11);
        private readonly SampleTestCommand message3 = new SampleTestCommand(12);

        private readonly SampleAggregateTestCommand succeededMessageAggregate1 = new SampleAggregateTestCommand(new SampleAggregateId(1));
        private readonly SampleAggregateTestCommand failedMessageAggregate2 = new SampleAggregateTestCommand(new SampleAggregateId(1));
        private readonly SampleAggregateTestCommand messageAggregate3 = new SampleAggregateTestCommand(new SampleAggregateId(1));

        private void GenerateData()
        {
            message1.SetContextData(MessagesConstants.UserId, "abba");
            _tracker.Started(message1);
            message2.SetContextData(MessagesConstants.UserId, "abba");
            _tracker.Started(message2);
            _tracker.Completed(message2, DateTime.UtcNow);

            message3.SetContextData(MessagesConstants.UserId, "batta");
            _tracker.Started(message3);
            _tracker.Completed(message3, DateTime.UtcNow);

            succeededMessageAggregate1.SetContextData(MessagesConstants.UserId, "adda");
            _tracker.Started(succeededMessageAggregate1);
            _tracker.Completed(succeededMessageAggregate1, DateTime.UtcNow);

            failedMessageAggregate2.SetContextData(MessagesConstants.UserId, "adda");
            _tracker.Started(failedMessageAggregate2);
            _tracker.Failed(failedMessageAggregate2, DateTime.UtcNow, new NotSupportedException());

            messageAggregate3.SetContextData(MessagesConstants.UserId, "1234");
            _tracker.Started(messageAggregate3);
        }

        [Test]
        public void Test_basic_get_by_id_list()
        {
            var byId = _tracker.GetByIdList(new List<string>() { message1.MessageId.ToString() });
            Assert.That(byId.Count, Is.EqualTo(1));
            Assert.That(byId[0].Completed != true);
        }

        [Test]
        public void Test_basic_get_by_user()
        {
            var byId = _tracker.GetCommands("abba", 1, 100);
            Assert.That(byId.TotalPages, Is.EqualTo(1));
            Assert.That(byId.Commands.Count, Is.EqualTo(2));
            Assert.That(byId.Commands[0].Completed == true);
        }

        [Test]
        public void Test_basic_get_by_aggregate()
        {
            var query = new MessageTrackerQuery();
            query.ForAggregate(succeededMessageAggregate1.AggregateId);
            var byAggregate = _tracker.Query(query, 100);
            Assert.That(byAggregate.Count, Is.EqualTo(3));
            Assert.That(byAggregate[0].MessageId, Is.EqualTo(messageAggregate3.MessageId.ToString()));
        }

        [Test]
        public void Test_basic_get_by_aggregate_failed()
        {
            var query = new MessageTrackerQuery();
            query
                .ForAggregate(succeededMessageAggregate1.AggregateId)
                .GetFailed();
            var byAggregate = _tracker.Query(query, 100);
            Assert.That(byAggregate.Count, Is.EqualTo(1));
            Assert.That(byAggregate[0].MessageId, Is.EqualTo(failedMessageAggregate2.MessageId.ToString()));
        }

        [Test]
        public void Test_basic_get_by_aggregate_success()
        {
            var query = new MessageTrackerQuery();
            query
                .ForAggregate(succeededMessageAggregate1.AggregateId)
                .GetSucceeded();
            var byAggregate = _tracker.Query(query, 100);
            Assert.That(byAggregate.Count, Is.EqualTo(1));
            Assert.That(byAggregate[0].MessageId, Is.EqualTo(succeededMessageAggregate1.MessageId.ToString()));
        }

        [Test]
        public void Test_basic_get_by_aggregate_user()
        {
            var query = new MessageTrackerQuery();
            query
                .ForAggregate(succeededMessageAggregate1.AggregateId)
                .ForUser("1234");
            var byAggregate = _tracker.Query(query, 100);
            Assert.That(byAggregate.Count, Is.EqualTo(1));
            Assert.That(byAggregate[0].MessageId, Is.EqualTo(messageAggregate3.MessageId.ToString()));
        }
    }
}

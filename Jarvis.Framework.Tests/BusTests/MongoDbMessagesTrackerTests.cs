using System;
using NUnit.Framework;
using System.Configuration;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Driver;
using System.Linq;
using Jarvis.Framework.Tests.BusTests.MessageFolder;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class MongoDbMessagesTrackerTests
    {
        MongoDbMessagesTracker sut;
        IMongoCollection<TrackedMessageModel> _messages;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            String connectionString = ConfigurationManager.ConnectionStrings["log"].ConnectionString;
            var logUrl = new MongoUrl(connectionString);
            var logClient = new MongoClient(logUrl);
            var logDb = logClient.GetDatabase(logUrl.DatabaseName);
            sut = new MongoDbMessagesTracker(logDb);
            _messages = logDb.GetCollection<TrackedMessageModel>("messages");
        }

        [SetUp]
        public void SetUp()
        {
            sut.Drop();
        }

        [Test]
        public void Verify_multiple_start_execution_time()
        {
            SampleTestCommand cmd = new SampleTestCommand(1);
            sut.Started(cmd);
            DateTime startDate1 = new DateTime(2000, 01, 01, 1, 1, 42, DateTimeKind.Utc);
            DateTime startDate2 = startDate1.AddSeconds(1);
            sut.ElaborationStarted(cmd, startDate1);
            sut.ElaborationStarted(cmd, startDate2);

            var handledTrack = _messages.AsQueryable().Single(t => t.MessageId == cmd.MessageId.ToString());
            Assert.That(handledTrack.LastExecutionStartTime, Is.EqualTo(startDate2));
            Assert.That(handledTrack.ExecutionStartTimeList, Is.EquivalentTo(new[] { startDate1, startDate2 }));
        }
    }
}

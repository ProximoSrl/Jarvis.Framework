using Jarvis.Framework.Shared.Commands.Tracking;
using Jarvis.Framework.Shared.Support;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Configuration;
using System.Linq;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    public class MongoDbMessagesTrackerTests
    {
        private MongoDbMessagesTracker sut;
        private IMongoCollection<TrackedMessageModel> _messages;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            String connectionString = ConfigurationManager.ConnectionStrings["log"].ConnectionString;
            var logUrl = new MongoUrl(connectionString);
            var logClient = logUrl.CreateClient(false);
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

        [Test]
        public void Verify_multiple_start_do_not_push_too_much_data()
        {
            SampleTestCommand cmd = new SampleTestCommand(1);
            sut.Started(cmd);
            DateTime startDate1 = new DateTime(2000, 01, 01, 1, 1, 42, DateTimeKind.Utc);
            for (int i = 0; i < 100; i++)
            {
                sut.ElaborationStarted(cmd, startDate1.AddMinutes(i));
            }

            var handledTrack = _messages.AsQueryable().Single(t => t.MessageId == cmd.MessageId.ToString());
            Assert.That(handledTrack.ExecutionStartTimeList.Length, Is.EqualTo(10));
            var last = handledTrack.ExecutionStartTimeList.Last();
            Assert.That(last, Is.EqualTo(startDate1.AddMinutes(99)));
        }
    }
}

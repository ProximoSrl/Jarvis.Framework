using Jarvis.Framework.Shared.Commands.Tracking;
using Jarvis.Framework.Shared.Support;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Configuration;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Commands;

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

        [Test]
        public async Task TrackBatchAsync_inserts_records_with_instant_timestamps()
        {
            SampleTestCommand cmd1 = new SampleTestCommand(1);
            SampleTestCommand cmd2 = new SampleTestCommand(2);
            await sut.TrackBatchAsync(new List<ICommand> { cmd1, cmd2 }, cancellationToken: CancellationToken.None);

            var tracks = _messages.Find(_ => true).ToList();
            Assert.That(tracks.Count, Is.EqualTo(2));

            var t1 = tracks.Single(t => t.MessageId == cmd1.MessageId.ToString());
            Assert.That(t1.Completed, Is.True);
            Assert.That(t1.Success, Is.True);
            Assert.That(t1.ExecutionStartTimeList.Length, Is.EqualTo(1));
            Assert.That(t1.LastExecutionStartTime, Is.EqualTo(t1.ExecutionStartTimeList[0]));
            Assert.That(t1.CompletedAt, Is.EqualTo(t1.ExecutionStartTimeList[0]));
            Assert.That(t1.ExecutionCount, Is.EqualTo(1));
            Assert.That((t1.ExpireDate.Value - t1.CompletedAt.Value).TotalDays, Is.GreaterThan(29));
        }

        [Test]
        public async Task TrackBatchAsync_updates_existing_started_record_to_completed()
        {
            SampleTestCommand cmd = new SampleTestCommand(1);
            sut.Started(cmd);
            var started = _messages.AsQueryable().Single(t => t.MessageId == cmd.MessageId.ToString());
            var startedAt = started.StartedAt;

            await sut.TrackBatchAsync(new List<ICommand> { cmd }, cancellationToken: CancellationToken.None);

            var updated = _messages.AsQueryable().Single(t => t.MessageId == cmd.MessageId.ToString());
            Assert.That(updated.StartedAt, Is.EqualTo(startedAt));
            Assert.That(updated.Completed, Is.True);
            Assert.That(updated.ExecutionStartTimeList.Length, Is.EqualTo(1));
            Assert.That(updated.ExecutionCount, Is.EqualTo(1));
        }

        [Test]
        public async Task TrackBatchAsync_empty_list_is_noop()
        {
            await sut.TrackBatchAsync(new List<ICommand>(), cancellationToken: CancellationToken.None);
            var tracks = _messages.Find(_ => true).ToList();
            Assert.That(tracks.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task TrackBatchAsync_records_failed_commands()
        {
            SampleTestCommand cmdOk = new SampleTestCommand(1);
            SampleTestCommand cmdFail = new SampleTestCommand(2);

            var failure = new FailedCommandInfo(cmdFail, "boom", new InvalidOperationException("boom"));

            await sut.TrackBatchAsync(new List<ICommand> { cmdOk, cmdFail }, new List<FailedCommandInfo> { failure }, cancellationToken: CancellationToken.None);

            var tOk = _messages.AsQueryable().Single(t => t.MessageId == cmdOk.MessageId.ToString());
            Assert.That(tOk.Success, Is.True);
            Assert.That(tOk.Completed, Is.True);

            var tFail = _messages.AsQueryable().Single(t => t.MessageId == cmdFail.MessageId.ToString());
            Assert.That(tFail.Success, Is.False);
            Assert.That(tFail.Completed, Is.True);
            Assert.That(tFail.ErrorMessage, Is.EqualTo("boom"));
            Assert.That(tFail.FullException, Does.Contain("InvalidOperationException"));
            Assert.That((tFail.ExpireDate.Value - tFail.CompletedAt.Value).TotalDays, Is.GreaterThan(365 * 6));
        }
    }
}

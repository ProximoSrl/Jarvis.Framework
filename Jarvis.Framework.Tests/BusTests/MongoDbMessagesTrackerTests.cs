using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Commands;
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
            //The dispatch tracking flag is a global static: make sure every test starts from the default (disabled).
            JarvisFrameworkGlobalConfiguration.DisableTrackMessageDispatched();
        }

        [TearDown]
        public void TearDown()
        {
            //Never let the global flag leak into other tests/fixtures.
            JarvisFrameworkGlobalConfiguration.DisableTrackMessageDispatched();
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

        [Test]
        public async Task TrackBatchAsync_with_parallel_options_inserts_all_records()
        {
            // Arrange: Create 10 commands
            var commands = new List<ICommand>();
            for (int i = 1; i <= 10; i++)
            {
                commands.Add(new SampleTestCommand(i));
            }

            var options = new BatchWriteOptions { DegreeOfParallelism = 3 };

            // Act
            await sut.TrackBatchAsync(commands, batchWriteOptions: options, cancellationToken: CancellationToken.None);

            // Assert: All commands were tracked
            var tracks = _messages.Find(_ => true).ToList();
            Assert.That(tracks.Count, Is.EqualTo(10));

            foreach (var cmd in commands)
            {
                var track = tracks.Single(t => t.MessageId == cmd.MessageId.ToString());
                Assert.That(track.Completed, Is.True);
                Assert.That(track.Success, Is.True);
                Assert.That(track.ExecutionCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void Dispatched_does_not_write_by_default()
        {
            SampleTestCommand cmd = new SampleTestCommand(1);
            sut.Started(cmd);

            var dispatchedAt = new DateTime(2020, 01, 01, 12, 0, 0, DateTimeKind.Utc);
            var result = sut.Dispatched(cmd.MessageId, dispatchedAt);

            //Default behavior: nothing is persisted but the call still reports success to the caller.
            Assert.That(result, Is.True);

            var track = _messages.AsQueryable().Single(t => t.MessageId == cmd.MessageId.ToString());
            Assert.That(track.DispatchedAt, Is.Null, "DispatchedAt must not be written when dispatch tracking is disabled");
        }

        [Test]
        public void Dispatched_does_not_write_even_if_record_missing_by_default()
        {
            //When disabled the method must be a pure no-op: it must not touch mongo at all, so it must
            //not upsert or create any document for an unknown message id.
            var result = sut.Dispatched(Guid.NewGuid(), new DateTime(2020, 01, 01, 12, 0, 0, DateTimeKind.Utc));

            Assert.That(result, Is.True);
            Assert.That(_messages.Find(_ => true).ToList(), Is.Empty, "No document should be created when dispatch tracking is disabled");
        }

        [Test]
        public void Dispatched_writes_when_tracking_enabled()
        {
            JarvisFrameworkGlobalConfiguration.EnableTrackMessageDispatched();

            SampleTestCommand cmd = new SampleTestCommand(1);
            sut.Started(cmd);

            var dispatchedAt = new DateTime(2020, 01, 01, 12, 0, 0, DateTimeKind.Utc);
            var result = sut.Dispatched(cmd.MessageId, dispatchedAt);

            Assert.That(result, Is.True, "First dispatch should report that the record was modified");

            var track = _messages.AsQueryable().Single(t => t.MessageId == cmd.MessageId.ToString());
            Assert.That(track.DispatchedAt, Is.EqualTo(dispatchedAt));
        }

        [Test]
        public void Dispatched_when_tracking_enabled_is_idempotent()
        {
            JarvisFrameworkGlobalConfiguration.EnableTrackMessageDispatched();

            SampleTestCommand cmd = new SampleTestCommand(1);
            sut.Started(cmd);

            var dispatchedAt = new DateTime(2020, 01, 01, 12, 0, 0, DateTimeKind.Utc);
            var firstResult = sut.Dispatched(cmd.MessageId, dispatchedAt);
            var secondResult = sut.Dispatched(cmd.MessageId, dispatchedAt.AddMinutes(5));

            Assert.That(firstResult, Is.True, "First dispatch should modify the record");
            Assert.That(secondResult, Is.False, "A message already dispatched should not be modified again");

            var track = _messages.AsQueryable().Single(t => t.MessageId == cmd.MessageId.ToString());
            Assert.That(track.DispatchedAt, Is.EqualTo(dispatchedAt), "DispatchedAt must keep the first dispatch value");
        }
    }
}

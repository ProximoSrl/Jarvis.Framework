using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class ConcurrentCheckpointTrackerBulkWriteTests
    {
        [Test]
        public void Bulk_write_failure_is_propagated_to_the_caller()
        {
            var collection = Substitute.For<IMongoCollection<Checkpoint>>();
            var expectedFailure = new TimeoutException("bulk failed");
            collection
                .BulkWriteAsync(
                    Arg.Any<IEnumerable<WriteModel<Checkpoint>>>(),
                    Arg.Any<BulkWriteOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromException<BulkWriteResult<Checkpoint>>(expectedFailure));
            var sut = new ConcurrentCheckpointTracker(collection, -1);

            var actualFailure = Assert.ThrowsAsync<TimeoutException>(async () =>
                await sut.UpdateSlotAndSetCheckpointAsync("slot", new[] { "projection" }, 42, true)
                    .WaitAsync(TimeSpan.FromSeconds(2))
                    .ConfigureAwait(false));

            Assert.That(actualFailure, Is.SameAs(expectedFailure));
            collection.Received(1).BulkWriteAsync(
                Arg.Any<IEnumerable<WriteModel<Checkpoint>>>(),
                Arg.Is<BulkWriteOptions>(options => !options.IsOrdered),
                CancellationToken.None);
        }

        [Test]
        public async Task Deferred_checkpoint_arriving_during_flush_is_retained_for_next_flush()
        {
            var bulkEntered = NewCompletionSource();
            var releaseBulk = NewCompletionSource();
            var bulkCalls = 0;
            var collection = Substitute.For<IMongoCollection<Checkpoint>>();
            collection
                .BulkWriteAsync(
                    Arg.Any<IEnumerable<WriteModel<Checkpoint>>>(),
                    Arg.Any<BulkWriteOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns<Task<BulkWriteResult<Checkpoint>>>(async callInfo =>
                {
                    var models = callInfo.Arg<IEnumerable<WriteModel<Checkpoint>>>().ToList();
                    if (Interlocked.Increment(ref bulkCalls) == 1)
                    {
                        bulkEntered.SetResult(true);
                        await releaseBulk.Task.ConfigureAwait(false);
                    }

                    return new BulkWriteResult<Checkpoint>.Acknowledged(
                        models.Count,
                        models.Count,
                        0,
                        0,
                        models.Count,
                        models,
                        Array.Empty<BulkWriteUpsert>());
                });
            var sut = new ConcurrentCheckpointTracker(
                collection,
                60,
                TimeSpan.Zero);

            await sut.UpdateSlotAndSetCheckpointAsync(
                "slot",
                new[] { "projection" },
                100,
                false).ConfigureAwait(false);
            // FlushCheckpointAsync waits for the durable write it enqueues, so it
            // cannot return while the test holds the first bulk open. Run it on a
            // separate task so the test can enqueue the racing checkpoint meanwhile.
            var flush = Task.Run(() => sut.FlushCheckpointAsync());
            await bulkEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            await sut.UpdateSlotAndSetCheckpointAsync(
                "slot",
                new[] { "projection" },
                200,
                false).ConfigureAwait(false);
            releaseBulk.SetResult(true);
            await flush.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            await sut.FlushCheckpointAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            Assert.That(bulkCalls, Is.EqualTo(2));
            collection.Received(2).BulkWriteAsync(
                Arg.Any<IEnumerable<WriteModel<Checkpoint>>>(),
                Arg.Is<BulkWriteOptions>(options => !options.IsOrdered),
                CancellationToken.None);
        }

        [Test]
        public async Task Failed_deferred_flush_remains_pending_for_next_flush()
        {
            var bulkCalls = 0;
            var expectedFailure = new TimeoutException("first deferred flush failed");
            var collection = Substitute.For<IMongoCollection<Checkpoint>>();
            collection
                .BulkWriteAsync(
                    Arg.Any<IEnumerable<WriteModel<Checkpoint>>>(),
                    Arg.Any<BulkWriteOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var models = callInfo.Arg<IEnumerable<WriteModel<Checkpoint>>>().ToList();
                    if (Interlocked.Increment(ref bulkCalls) == 1)
                    {
                        return Task.FromException<BulkWriteResult<Checkpoint>>(expectedFailure);
                    }

                    return Task.FromResult<BulkWriteResult<Checkpoint>>(new BulkWriteResult<Checkpoint>.Acknowledged(
                        models.Count,
                        models.Count,
                        0,
                        0,
                        models.Count,
                        models,
                        Array.Empty<BulkWriteUpsert>()));
                });
            var sut = new ConcurrentCheckpointTracker(
                collection,
                60,
                TimeSpan.Zero);

            await sut.UpdateSlotAndSetCheckpointAsync(
                "slot",
                new[] { "projection" },
                100,
                false).ConfigureAwait(false);

            // FlushCheckpointAsync contains and logs persistence failures. The
            // second call proves the failed deferred request was restored.
            await sut.FlushCheckpointAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            await sut.FlushCheckpointAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            Assert.That(bulkCalls, Is.EqualTo(2));
            collection.Received(2).BulkWriteAsync(
                Arg.Any<IEnumerable<WriteModel<Checkpoint>>>(),
                Arg.Is<BulkWriteOptions>(options => !options.IsOrdered),
                CancellationToken.None);
        }

        [Test]
        public async Task Indexed_write_error_fails_only_the_rejected_slot_when_the_rest_are_acknowledged()
        {
            var database = CreateDatabase();
            var collectionName = $"checkpoint-bulk-validation-{Guid.NewGuid():N}";
            var validator = new BsonDocument("$expr", new BsonDocument(
                "$lte",
                new BsonArray { "$Current", 10L }));

            database.CreateCollection(
                collectionName,
                new CreateCollectionOptions<Checkpoint>
                {
                    Validator = new BsonDocumentFilterDefinition<Checkpoint>(validator)
                });

            try
            {
                var collection = database.GetCollection<Checkpoint>(collectionName);
                collection.InsertMany(new[]
                {
                    CreateCheckpoint("projection-ok", "slot-ok"),
                    CreateCheckpoint("projection-rejected", "slot-rejected")
                });
                // Keep the window open until both requests are queued, then close it explicitly.
                // This proves indexed-error handling without depending on the production 1 ms timer.
                var sut = new ConcurrentCheckpointTracker(
                    collection,
                    -1,
                    TimeSpan.FromSeconds(30));

                var accepted = sut.UpdateSlotAndSetCheckpointAsync(
                    "slot-ok",
                    new[] { "projection-ok" },
                    5,
                    true);
                var rejected = sut.UpdateSlotAndSetCheckpointAsync(
                    "slot-rejected",
                    new[] { "projection-rejected" },
                    20,
                    true);

                await sut.FlushCheckpointAsync()
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
                await accepted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                var failure = Assert.ThrowsAsync<MongoBulkWriteException<Checkpoint>>(async () =>
                    await rejected.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false));

                Assert.That(failure.WriteConcernError, Is.Null);
                Assert.That(failure.UnprocessedRequests, Is.Empty);
                Assert.That(failure.WriteErrors, Has.Count.EqualTo(1));
                Assert.That(failure.WriteErrors.Single().Index, Is.InRange(0, 1));
                Assert.That(collection.Find(Builders<Checkpoint>.Filter.Eq(checkpoint => checkpoint.Slot, "slot-ok")).Single().Current, Is.EqualTo(5));
                Assert.That(collection.Find(Builders<Checkpoint>.Filter.Eq(checkpoint => checkpoint.Slot, "slot-rejected")).Single().Current, Is.EqualTo(0));
            }
            finally
            {
                database.DropCollection(collectionName);
            }
        }

        [Test]
        public void Write_concern_error_never_allows_selective_success()
        {
            var canIdentifySuccessfulRequests = ConcurrentCheckpointTracker.CanIdentifySuccessfulRequests(
                resultIsAcknowledged: true,
                resultRequestCount: 2,
                indexedErrorIndexes: new[] { 1 },
                hasWriteConcernError: true,
                unprocessedRequestCount: 0,
                expectedRequestCount: 2);

            Assert.That(canIdentifySuccessfulRequests, Is.False);
        }

        [Test]
        public void Unprocessed_request_never_allows_selective_success()
        {
            var canIdentifySuccessfulRequests = ConcurrentCheckpointTracker.CanIdentifySuccessfulRequests(
                resultIsAcknowledged: true,
                resultRequestCount: 2,
                indexedErrorIndexes: new[] { 1 },
                hasWriteConcernError: false,
                unprocessedRequestCount: 1,
                expectedRequestCount: 2);

            Assert.That(canIdentifySuccessfulRequests, Is.False);
        }

        [Test]
        public void Unacknowledged_result_never_allows_selective_success()
        {
            var canIdentifySuccessfulRequests = CanIdentifySuccessfulRequests(
                resultIsAcknowledged: false,
                resultRequestCount: 2,
                indexedErrorIndexes: new[] { 1 },
                expectedRequestCount: 2);

            Assert.That(canIdentifySuccessfulRequests, Is.False);
        }

        [Test]
        public void Request_count_mismatch_never_allows_selective_success()
        {
            var canIdentifySuccessfulRequests = CanIdentifySuccessfulRequests(
                resultIsAcknowledged: true,
                resultRequestCount: 1,
                indexedErrorIndexes: new[] { 0 },
                expectedRequestCount: 2);

            Assert.That(canIdentifySuccessfulRequests, Is.False);
        }

        [Test]
        public void Missing_indexed_errors_never_allows_selective_success()
        {
            var canIdentifySuccessfulRequests = CanIdentifySuccessfulRequests(
                resultIsAcknowledged: true,
                resultRequestCount: 2,
                indexedErrorIndexes: Array.Empty<int>(),
                expectedRequestCount: 2);

            Assert.That(canIdentifySuccessfulRequests, Is.False);
        }

        [Test]
        public void Negative_error_index_never_allows_selective_success()
        {
            var canIdentifySuccessfulRequests = CanIdentifySuccessfulRequests(
                resultIsAcknowledged: true,
                resultRequestCount: 2,
                indexedErrorIndexes: new[] { -1 },
                expectedRequestCount: 2);

            Assert.That(canIdentifySuccessfulRequests, Is.False);
        }

        [Test]
        public void Out_of_range_error_index_never_allows_selective_success()
        {
            var canIdentifySuccessfulRequests = CanIdentifySuccessfulRequests(
                resultIsAcknowledged: true,
                resultRequestCount: 2,
                indexedErrorIndexes: new[] { 2 },
                expectedRequestCount: 2);

            Assert.That(canIdentifySuccessfulRequests, Is.False);
        }

        [Test]
        public void Duplicate_error_indexes_never_allow_selective_success()
        {
            var canIdentifySuccessfulRequests = CanIdentifySuccessfulRequests(
                resultIsAcknowledged: true,
                resultRequestCount: 3,
                indexedErrorIndexes: new[] { 1, 1 },
                expectedRequestCount: 3);

            Assert.That(canIdentifySuccessfulRequests, Is.False);
        }

        [Test]
        public void Multiple_distinct_in_range_error_indexes_allow_selective_success()
        {
            var canIdentifySuccessfulRequests = CanIdentifySuccessfulRequests(
                resultIsAcknowledged: true,
                resultRequestCount: 3,
                indexedErrorIndexes: new[] { 0, 2 },
                expectedRequestCount: 3);

            Assert.That(canIdentifySuccessfulRequests, Is.True);
        }

        private static bool CanIdentifySuccessfulRequests(
            bool resultIsAcknowledged,
            int resultRequestCount,
            IReadOnlyCollection<int> indexedErrorIndexes,
            int expectedRequestCount)
        {
            return ConcurrentCheckpointTracker.CanIdentifySuccessfulRequests(
                resultIsAcknowledged,
                resultRequestCount,
                indexedErrorIndexes,
                hasWriteConcernError: false,
                unprocessedRequestCount: 0,
                expectedRequestCount);
        }

        private static IMongoDatabase CreateDatabase()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString;
            var url = new MongoUrl(connectionString);
            return new MongoClient(connectionString).GetDatabase(url.DatabaseName);
        }

        private static Checkpoint CreateCheckpoint(string projectionName, string slotName)
        {
            return new Checkpoint(projectionName, 0, "signature")
            {
                Slot = slotName,
                Current = 0,
                Value = 0
            };
        }

        private static TaskCompletionSource<bool> NewCompletionSource()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}

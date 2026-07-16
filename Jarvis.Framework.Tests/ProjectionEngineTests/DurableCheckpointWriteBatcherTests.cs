using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class DurableCheckpointWriteBatcherTests
    {
        private static readonly IReadOnlyDictionary<int, Exception> NoFailures =
            new Dictionary<int, Exception>();

        [Test]
        public async Task Five_distinct_slots_share_one_bulk_write()
        {
            var batches = new ConcurrentQueue<IReadOnlyList<DurableCheckpointWriteBatcher.DurableCheckpointWrite>>();
            var sut = CreateBatcher(TimeSpan.FromMilliseconds(25), writes =>
            {
                batches.Enqueue(writes.ToArray());
                return Task.FromResult(NoFailures);
            });

            var writes = Enumerable.Range(1, 5)
                .Select(index => sut.EnqueueAsync($"slot-{index}", index))
                .ToArray();

            await Task.WhenAll(writes).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            Assert.That(batches, Has.Count.EqualTo(1));
            Assert.That(
                batches.Single().Select(write => write.SlotName),
                Is.EquivalentTo(Enumerable.Range(1, 5).Select(index => $"slot-{index}")));
        }

        [Test]
        public async Task Sequential_writes_for_same_slot_are_never_coalesced()
        {
            var firstBatchEntered = NewCompletionSource();
            var releaseFirstBatch = NewCompletionSource();
            var batches = new ConcurrentQueue<long[]>();
            var invocation = 0;
            var sut = CreateBatcher(TimeSpan.FromMilliseconds(1), async writes =>
            {
                batches.Enqueue(writes.Select(write => write.CheckpointToken).ToArray());
                if (Interlocked.Increment(ref invocation) == 1)
                {
                    firstBatchEntered.SetResult(true);
                    await releaseFirstBatch.Task.ConfigureAwait(false);
                }

                return NoFailures;
            });

            var first = sut.EnqueueAsync("same-slot", 10);
            await firstBatchEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            var second = sut.EnqueueAsync("same-slot", 11);

            Assert.That(batches, Has.Count.EqualTo(1));
            Assert.That(batches.Single(), Is.EqualTo(new long[] { 10 }));
            Assert.That(first.IsCompleted, Is.False, "The first caller must wait for its MongoDB acknowledgement.");
            Assert.That(second.IsCompleted, Is.False, "A later same-slot caller must wait behind the first durable write.");

            releaseFirstBatch.SetResult(true);
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            Assert.That(batches, Has.Count.EqualTo(2));
            Assert.That(batches.Last(), Is.EqualTo(new long[] { 11 }));
        }

        [Test]
        public async Task Indexed_bulk_failure_fails_only_its_request()
        {
            var expectedFailure = new InvalidOperationException("slot-3 failed");
            var sut = CreateBatcher(TimeSpan.FromMilliseconds(25), writes =>
                Task.FromResult<IReadOnlyDictionary<int, Exception>>(
                    new Dictionary<int, Exception> { [2] = expectedFailure }));

            var writes = Enumerable.Range(1, 5)
                .Select(index => sut.EnqueueAsync($"slot-{index}", index))
                .ToArray();

            var actualFailure = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await writes[2].WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false));
            await Task.WhenAll(writes.Where((_, index) => index != 2))
                .WaitAsync(TimeSpan.FromSeconds(2))
                .ConfigureAwait(false);

            Assert.That(actualFailure, Is.SameAs(expectedFailure));
            Assert.That(writes.Count(write => write.IsCompletedSuccessfully), Is.EqualTo(4));
        }

        [Test]
        public async Task Same_slot_head_after_failure_runs_in_a_separate_later_batch()
        {
            var expectedFailure = new InvalidOperationException("first head failed");
            var batches = new ConcurrentQueue<long[]>();
            var invocation = 0;
            var sut = CreateBatcher(TimeSpan.FromMilliseconds(25), writes =>
            {
                batches.Enqueue(writes.Select(write => write.CheckpointToken).ToArray());
                var failures = Interlocked.Increment(ref invocation) == 1
                    ? new Dictionary<int, Exception> { [0] = expectedFailure }
                    : NoFailures;
                return Task.FromResult<IReadOnlyDictionary<int, Exception>>(failures);
            });

            var first = sut.EnqueueAsync("same-slot", 10);
            var second = sut.EnqueueAsync("same-slot", 11);

            var actualFailure = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await first.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false));
            await second.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            Assert.That(actualFailure, Is.SameAs(expectedFailure));
            Assert.That(batches, Has.Count.EqualTo(2));
            Assert.That(batches.First(), Is.EqualTo(new long[] { 10 }));
            Assert.That(batches.Last(), Is.EqualTo(new long[] { 11 }));
        }

        [Test]
        public async Task Ambiguous_batch_failure_fails_every_request()
        {
            var expectedFailure = new TimeoutException("MongoDB acknowledgement is ambiguous");
            var sut = CreateBatcher(TimeSpan.FromMilliseconds(25), _ => Task.FromException<IReadOnlyDictionary<int, Exception>>(expectedFailure));

            var writes = Enumerable.Range(1, 5)
                .Select(index => sut.EnqueueAsync($"slot-{index}", index))
                .ToArray();

            foreach (var write in writes)
            {
                var actualFailure = Assert.ThrowsAsync<TimeoutException>(async () =>
                    await write.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false));
                Assert.That(actualFailure, Is.SameAs(expectedFailure));
            }
        }

        [Test]
        public async Task Reader_fault_fails_every_waiter_and_rejects_later_commands()
        {
            var expectedFailure = new InvalidOperationException("checkpoint ordering invariant failed");
            var sut = CreateBatcher(
                TimeSpan.FromSeconds(5),
                _ => Task.FromResult<IReadOnlyDictionary<int, Exception>>(
                    new ThrowingFailures(expectedFailure)));

            var writes = new[]
            {
                sut.EnqueueAsync("slot-1", 1),
                sut.EnqueueAsync("slot-1", 2),
                sut.EnqueueAsync("slot-2", 3)
            };
            var drain = sut.DrainAsync();

            var failureAssertions = writes.Select(async write =>
            {
                var actualFailure = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await write.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false));
                Assert.That(actualFailure, Is.SameAs(expectedFailure));
            });
            var drainFailure = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await drain.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false));
            await Task.WhenAll(failureAssertions)
                .WaitAsync(TimeSpan.FromSeconds(2))
                .ConfigureAwait(false);
            Assert.That(drainFailure, Is.SameAs(expectedFailure));

            var laterWriteFailure = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await sut.EnqueueAsync("slot-after-fault", 4)
                    .WaitAsync(TimeSpan.FromSeconds(2))
                    .ConfigureAwait(false));
            var laterDrainFailure = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await sut.DrainAsync()
                    .WaitAsync(TimeSpan.FromSeconds(2))
                    .ConfigureAwait(false));

            Assert.That(laterWriteFailure, Is.SameAs(expectedFailure));
            Assert.That(laterDrainFailure, Is.SameAs(expectedFailure));
        }

        [Test]
        public void Empty_failure_dictionary_is_immutable()
        {
            var failures = DurableCheckpointWriteBatcher.EmptyFailures;

            Assert.That(failures, Is.Empty);
            Assert.That(
                () => ((IDictionary<int, Exception>)failures).Add(0, new Exception()),
                Throws.InstanceOf<NotSupportedException>());
        }

        [Test]
        public async Task Explicit_drain_closes_collection_window_and_includes_racing_writes()
        {
            var batches = new ConcurrentQueue<IReadOnlyList<DurableCheckpointWriteBatcher.DurableCheckpointWrite>>();
            var sut = CreateBatcher(TimeSpan.FromSeconds(5), writes =>
            {
                batches.Enqueue(writes.ToArray());
                return Task.FromResult(NoFailures);
            });

            var writes = Enumerable.Range(1, 3)
                .Select(index => sut.EnqueueAsync($"slot-{index}", index))
                .ToArray();
            var drain = sut.DrainAsync();

            await Task.WhenAll(writes.Append(drain)).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            Assert.That(batches, Has.Count.EqualTo(1));
            Assert.That(batches.Single(), Has.Count.EqualTo(3));
        }

        [Test]
        public async Task Drain_is_a_barrier_and_does_not_wait_for_later_writes()
        {
            var firstBatchEntered = NewCompletionSource();
            var releaseFirstBatch = NewCompletionSource();
            var laterBatchEntered = NewCompletionSource();
            var releaseLaterBatch = NewCompletionSource();
            var invocation = 0;
            var sut = CreateBatcher(TimeSpan.Zero, async _ =>
            {
                if (Interlocked.Increment(ref invocation) == 1)
                {
                    firstBatchEntered.SetResult(true);
                    await releaseFirstBatch.Task.ConfigureAwait(false);
                }
                else
                {
                    laterBatchEntered.SetResult(true);
                    await releaseLaterBatch.Task.ConfigureAwait(false);
                }

                return NoFailures;
            });

            var firstWrite = sut.EnqueueAsync("slot-before-barrier", 1);
            await firstBatchEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            var drain = sut.DrainAsync();
            var laterWrite = sut.EnqueueAsync("slot-after-barrier", 2);

            releaseFirstBatch.SetResult(true);
            await Task.WhenAll(firstWrite, drain)
                .WaitAsync(TimeSpan.FromSeconds(2))
                .ConfigureAwait(false);
            await laterBatchEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            Assert.That(laterWrite.IsCompleted, Is.False);
            releaseLaterBatch.SetResult(true);
            await laterWrite.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }

        [Test]
        public async Task Drain_waits_for_same_slot_write_queued_behind_active_head()
        {
            var firstBatchEntered = NewCompletionSource();
            var releaseFirstBatch = NewCompletionSource();
            var batches = new ConcurrentQueue<long[]>();
            var invocation = 0;
            var sut = CreateBatcher(TimeSpan.Zero, async writes =>
            {
                batches.Enqueue(writes.Select(write => write.CheckpointToken).ToArray());
                if (Interlocked.Increment(ref invocation) == 1)
                {
                    firstBatchEntered.SetResult(true);
                    await releaseFirstBatch.Task.ConfigureAwait(false);
                }

                return NoFailures;
            });

            var dispatchWrite = sut.EnqueueAsync("same-slot", 10);
            await firstBatchEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            var flushWrite = sut.EnqueueAsync("same-slot", 11);
            var drain = sut.DrainAsync();

            Assert.That(drain.IsCompleted, Is.False);
            releaseFirstBatch.SetResult(true);
            await Task.WhenAll(dispatchWrite, flushWrite, drain)
                .WaitAsync(TimeSpan.FromSeconds(2))
                .ConfigureAwait(false);

            Assert.That(batches, Has.Count.EqualTo(2));
            Assert.That(batches.First(), Is.EqualTo(new long[] { 10 }));
            Assert.That(batches.Last(), Is.EqualTo(new long[] { 11 }));
        }

        [Test]
        public async Task Canceling_a_waiter_does_not_cancel_queued_durable_write()
        {
            var batchEntered = NewCompletionSource();
            var releaseBatch = NewCompletionSource();
            var sut = CreateBatcher(TimeSpan.Zero, async _ =>
            {
                batchEntered.SetResult(true);
                await releaseBatch.Task.ConfigureAwait(false);
                return NoFailures;
            });

            var durableWrite = sut.EnqueueAsync("slot", 42);
            await batchEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.That(
                async () => await durableWrite.WaitAsync(cancellation.Token).ConfigureAwait(false),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(durableWrite.IsCompleted, Is.False);

            releaseBatch.SetResult(true);
            await durableWrite.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            Assert.That(durableWrite.IsCompletedSuccessfully, Is.True);
        }

        private static DurableCheckpointWriteBatcher CreateBatcher(
            TimeSpan collectionWindow,
            Func<IReadOnlyList<DurableCheckpointWriteBatcher.DurableCheckpointWrite>, Task<IReadOnlyDictionary<int, Exception>>> executeBatchAsync)
        {
            return new DurableCheckpointWriteBatcher(collectionWindow, executeBatchAsync);
        }

        private static TaskCompletionSource<bool> NewCompletionSource()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private sealed class ThrowingFailures : IReadOnlyDictionary<int, Exception>
        {
            private readonly Exception _exception;

            public ThrowingFailures(Exception exception)
            {
                _exception = exception;
            }

            public Exception this[int key] => throw _exception;

            public IEnumerable<int> Keys => Array.Empty<int>();

            public IEnumerable<Exception> Values => Array.Empty<Exception>();

            public int Count => 0;

            public bool ContainsKey(int key)
            {
                return false;
            }

            public bool TryGetValue(int key, out Exception value)
            {
                value = null;
                throw _exception;
            }

            public IEnumerator<KeyValuePair<int, Exception>> GetEnumerator()
            {
                return Enumerable.Empty<KeyValuePair<int, Exception>>().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}

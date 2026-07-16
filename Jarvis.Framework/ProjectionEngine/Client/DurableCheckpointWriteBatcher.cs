using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// Opportunistically groups durable checkpoint writes for distinct slots.
    /// Requests for the same slot are kept as separate, ordered batch heads.
    /// Projection at-least-once semantics are unchanged; batching extends the
    /// side-effect-to-durable-checkpoint interval by at most the collection window,
    /// plus time spent behind an earlier durable write for the same slot.
    /// </summary>
    internal sealed class DurableCheckpointWriteBatcher
    {
        private static readonly Meter Meter = new(ProjectionEngineTelemetry.MeterName);
        private static readonly Counter<long> SubmittedRequests = Meter.CreateCounter<long>(
            "jarvis.framework.checkpoint.write.requests",
            "{request}");
        private static readonly Counter<long> MongoBulkCalls = Meter.CreateCounter<long>(
            "jarvis.framework.checkpoint.write.mongo.bulk.calls",
            "{call}");
        private static readonly Counter<long> FailedRequests = Meter.CreateCounter<long>(
            "jarvis.framework.checkpoint.write.failures",
            "{request}");
        private static readonly Counter<long> Flushes = Meter.CreateCounter<long>(
            "jarvis.framework.checkpoint.write.flushes",
            "{flush}");
        private static readonly Histogram<int> BatchSize = Meter.CreateHistogram<int>(
            "jarvis.framework.checkpoint.write.batch.size",
            "{request}");
        private static readonly Histogram<double> EnqueueToAcknowledgement = Meter.CreateHistogram<double>(
            "jarvis.framework.checkpoint.write.enqueue_to_ack.duration",
            "ms");
        private static readonly Histogram<double> MongoDuration = Meter.CreateHistogram<double>(
            "jarvis.framework.checkpoint.write.mongo.duration",
            "ms");
        internal static readonly IReadOnlyDictionary<int, Exception> EmptyFailures =
            new ReadOnlyDictionary<int, Exception>(new Dictionary<int, Exception>());

        private readonly TimeSpan _collectionWindow;
        private readonly Func<IReadOnlyList<DurableCheckpointWrite>, Task<IReadOnlyDictionary<int, Exception>>> _executeBatchAsync;
        private readonly Channel<BatcherCommand> _commands;

        /// <summary>Used to keep a long running task alive </summary>
        private readonly Task _reader;
        private Exception _readerFault;

        public DurableCheckpointWriteBatcher(
            TimeSpan collectionWindow,
            Func<IReadOnlyList<DurableCheckpointWrite>, Task<IReadOnlyDictionary<int, Exception>>> executeBatchAsync)
        {
            if (collectionWindow < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(collectionWindow));
            }

            _collectionWindow = collectionWindow;
            _executeBatchAsync = executeBatchAsync ?? throw new ArgumentNullException(nameof(executeBatchAsync));
            _commands = Channel.CreateUnbounded<BatcherCommand>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
            _reader = RunAsync();
        }

        public Task EnqueueAsync(string slotName, long checkpointToken)
        {
            if (String.IsNullOrWhiteSpace(slotName))
            {
                throw new ArgumentException("A slot name is required.", nameof(slotName));
            }

            var request = new DurableCheckpointWrite(slotName, checkpointToken);
            SubmittedRequests.Add(1);
            if (!_commands.Writer.TryWrite(new WriteCommand(request)))
            {
                request.Completion.TrySetException(GetReaderFault());
            }

            return request.Completion.Task;
        }

        /// <summary>
        /// Closes the current collection window and waits for all commands that
        /// precede this barrier. Writes submitted after it do not extend the wait.
        /// </summary>
        public Task DrainAsync()
        {
            var barrier = new BarrierCommand();
            if (!_commands.Writer.TryWrite(barrier))
            {
                barrier.Completion.TrySetException(GetReaderFault());
            }

            return barrier.Completion.Task;
        }

        private async Task RunAsync()
        {
            var slotQueues = new Dictionary<string, Queue<DurableCheckpointWrite>>(StringComparer.Ordinal);
            BarrierCommand activeBarrier = null;
            try
            {
                while (true)
                {
                    if (slotQueues.Count == 0)
                    {
                        if (!await _commands.Reader.WaitToReadAsync().ConfigureAwait(false))
                        {
                            return;
                        }

                        if (!_commands.Reader.TryRead(out var command))
                        {
                            continue;
                        }

                        if (command is BarrierCommand barrier)
                        {
                            barrier.Completion.TrySetResult(true);
                            continue;
                        }

                        ProcessWrite(((WriteCommand)command).Request, slotQueues);
                    }

                    activeBarrier = await CollectCommandsAsync(slotQueues).ConfigureAwait(false);
                    var explicitFlush = activeBarrier != null;

                    do
                    {
                        await ExecuteHeadsAsync(slotQueues, explicitFlush).ConfigureAwait(false);

                        if (activeBarrier == null)
                        {
                            break;
                        }

                        // The reader stopped at the barrier, so every queued tail
                        // belongs before it. Drain them without waiting for another
                        // collection window; later channel commands remain unrelated.
                        explicitFlush = true;
                    }
                    while (slotQueues.Count != 0);

                    if (activeBarrier != null)
                    {
                        activeBarrier.Completion.TrySetResult(true);
                        activeBarrier = null;
                    }
                }
            }
            catch (Exception ex)
            {
                ContainReaderFault(slotQueues, activeBarrier, ex);
            }
        }

        private async Task<BarrierCommand> CollectCommandsAsync(
            Dictionary<string, Queue<DurableCheckpointWrite>> slotQueues)
        {
            using var collectionWindow = new CancellationTokenSource(_collectionWindow);
            while (true)
            {
                while (_commands.Reader.TryRead(out var command))
                {
                    if (command is BarrierCommand barrier)
                    {
                        return barrier;
                    }

                    ProcessWrite(((WriteCommand)command).Request, slotQueues);
                }

                if (_collectionWindow == TimeSpan.Zero)
                {
                    return null;
                }

                try
                {
                    if (!await _commands.Reader.WaitToReadAsync(collectionWindow.Token).ConfigureAwait(false))
                    {
                        return null;
                    }
                }
                catch (OperationCanceledException) when (collectionWindow.IsCancellationRequested)
                {
                    return null;
                }
            }
        }

        private static void ProcessWrite(
            DurableCheckpointWrite request,
            Dictionary<string, Queue<DurableCheckpointWrite>> slotQueues)
        {
            if (!slotQueues.TryGetValue(request.SlotName, out var queue))
            {
                queue = new Queue<DurableCheckpointWrite>();
                slotQueues.Add(request.SlotName, queue);
            }

            queue.Enqueue(request);
        }

        private async Task ExecuteHeadsAsync(
            Dictionary<string, Queue<DurableCheckpointWrite>> slotQueues,
            bool explicitFlush)
        {
            // Selecting Queue.Peek() takes exactly one head per slot. Same-slot
            // tails cannot enter this bulk and remain ordered behind its result.
            var batch = slotQueues.Values.Select(queue => queue.Peek()).ToArray();
            Flushes.Add(1, new KeyValuePair<string, object>(
                "reason",
                explicitFlush ? "explicit" : "window"));
            BatchSize.Record(batch.Length);
            MongoBulkCalls.Add(1);

            IReadOnlyDictionary<int, Exception> failures;
            var mongoStarted = Stopwatch.GetTimestamp();
            try
            {
                failures = await _executeBatchAsync(batch).ConfigureAwait(false)
                    ?? EmptyFailures;
            }
            catch (Exception ex)
            {
                failures = Enumerable.Range(0, batch.Length)
                    .ToDictionary(index => index, _ => ex);
            }
            finally
            {
                MongoDuration.Record(Stopwatch.GetElapsedTime(mongoStarted).TotalMilliseconds);
            }

            CompleteBatch(slotQueues, batch, failures);
        }

        private static void CompleteBatch(
            Dictionary<string, Queue<DurableCheckpointWrite>> slotQueues,
            IReadOnlyList<DurableCheckpointWrite> batch,
            IReadOnlyDictionary<int, Exception> failures)
        {
            for (var index = 0; index < batch.Count; index++)
            {
                var request = batch[index];
                var queue = slotQueues[request.SlotName];
                if (!ReferenceEquals(queue.Peek(), request))
                {
                    throw new InvalidOperationException($"Checkpoint write ordering was lost for slot '{request.SlotName}'.");
                }

                failures.TryGetValue(index, out var failure);
                queue.Dequeue();
                if (queue.Count == 0)
                {
                    slotQueues.Remove(request.SlotName);
                }

                if (failure != null)
                {
                    FailedRequests.Add(1);
                    request.Completion.TrySetException(failure);
                }
                else
                {
                    request.Completion.TrySetResult(true);
                }

                EnqueueToAcknowledgement.Record(
                    Stopwatch.GetElapsedTime(request.EnqueuedAtTimestamp).TotalMilliseconds);
            }
        }

        private void ContainReaderFault(
            Dictionary<string, Queue<DurableCheckpointWrite>> slotQueues,
            BarrierCommand activeBarrier,
            Exception exception)
        {
            Volatile.Write(ref _readerFault, exception);
            _commands.Writer.TryComplete(exception);

            foreach (var request in slotQueues.Values.SelectMany(queue => queue))
            {
                request.Completion.TrySetException(exception);
            }

            activeBarrier?.Completion.TrySetException(exception);
            while (_commands.Reader.TryRead(out var command))
            {
                switch (command)
                {
                    case WriteCommand write:
                        write.Request.Completion.TrySetException(exception);
                        break;
                    case BarrierCommand barrier:
                        barrier.Completion.TrySetException(exception);
                        break;
                }
            }
        }

        private Exception GetReaderFault()
        {
            return Volatile.Read(ref _readerFault)
                ?? new InvalidOperationException("The durable checkpoint writer is no longer available.");
        }

        private abstract class BatcherCommand
        {
        }

        private sealed class WriteCommand : BatcherCommand
        {
            public WriteCommand(DurableCheckpointWrite request)
            {
                Request = request;
            }

            public DurableCheckpointWrite Request { get; }
        }

        private sealed class BarrierCommand : BatcherCommand
        {
            public TaskCompletionSource<bool> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal sealed class DurableCheckpointWrite
        {
            public DurableCheckpointWrite(string slotName, long checkpointToken)
            {
                SlotName = slotName;
                CheckpointToken = checkpointToken;
                EnqueuedAtTimestamp = Stopwatch.GetTimestamp();
                Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public string SlotName { get; }

            public long CheckpointToken { get; }

            public long EnqueuedAtTimestamp { get; }

            public TaskCompletionSource<bool> Completion { get; }
        }
    }
}

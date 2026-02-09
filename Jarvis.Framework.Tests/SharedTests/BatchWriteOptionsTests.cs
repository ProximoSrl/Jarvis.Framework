using Jarvis.Framework.Shared;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.SharedTests
{
    [TestFixture]
    public class BatchWriteOptionsTests
    {
        [Test]
        public void Default_should_have_degree_of_parallelism_one()
        {
            var options = BatchWriteOptions.Default;
            Assert.That(options.DegreeOfParallelism, Is.EqualTo(1));
        }

        [Test]
        public void Validate_should_throw_on_zero_degree_of_parallelism()
        {
            var options = new BatchWriteOptions { DegreeOfParallelism = 0 };
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
            Assert.That(ex.ParamName, Is.EqualTo("DegreeOfParallelism"));
        }

        [Test]
        public void Validate_should_throw_on_negative_degree_of_parallelism()
        {
            var options = new BatchWriteOptions { DegreeOfParallelism = -1 };
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
            Assert.That(ex.ParamName, Is.EqualTo("DegreeOfParallelism"));
        }

        [Test]
        public void Validate_should_not_throw_for_valid_options()
        {
            var options = new BatchWriteOptions { DegreeOfParallelism = 4 };
            Assert.DoesNotThrow(() => options.Validate());
        }
    }

    [TestFixture]
    public class BatchWriteHelperTests
    {
        [Test]
        public async Task ExecuteInChunksAsync_with_default_options_executes_once()
        {
            var operations = new List<int> { 1, 2, 3, 4, 5 };
            int executionCount = 0;

            await BatchWriteHelper.ExecuteInChunksAsync(
                operations,
                null,
                (chunk, ct) =>
                {
                    Interlocked.Increment(ref executionCount);
                    Assert.That(chunk.Count, Is.EqualTo(5));
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.That(executionCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ExecuteInChunksAsync_splits_into_correct_number_of_chunks()
        {
            var operations = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int executionCount = 0;
            var chunkSizes = new List<int>();

            await BatchWriteHelper.ExecuteInChunksAsync(
                operations,
                new BatchWriteOptions { DegreeOfParallelism = 3 },
                (chunk, ct) =>
                {
                    Interlocked.Increment(ref executionCount);
                    lock (chunkSizes)
                    {
                        chunkSizes.Add(chunk.Count);
                    }
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.That(executionCount, Is.EqualTo(3));
            chunkSizes.Sort();
            // 10 items / 3 chunks = ceil(3.33) = 4 per chunk: 4, 4, 2
            Assert.That(chunkSizes, Is.EquivalentTo(new[] { 2, 4, 4 }));
        }

        [Test]
        public async Task ExecuteInChunksAsync_handles_empty_list()
        {
            var operations = new List<int>();
            int executionCount = 0;

            await BatchWriteHelper.ExecuteInChunksAsync(
                operations,
                new BatchWriteOptions { DegreeOfParallelism = 4 },
                (chunk, ct) =>
                {
                    Interlocked.Increment(ref executionCount);
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.That(executionCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ExecuteInChunksAsync_with_more_parallelism_than_items()
        {
            var operations = new List<int> { 1, 2 };
            int executionCount = 0;

            await BatchWriteHelper.ExecuteInChunksAsync(
                operations,
                new BatchWriteOptions { DegreeOfParallelism = 10 },
                (chunk, ct) =>
                {
                    Interlocked.Increment(ref executionCount);
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            // Each item gets its own chunk when parallelism > item count
            Assert.That(executionCount, Is.EqualTo(2));
        }

        [Test]
        public async Task ExecuteInChunksAsync_with_single_item_and_parallelism()
        {
            var operations = new List<int> { 42 };
            int executionCount = 0;
            int receivedValue = 0;

            await BatchWriteHelper.ExecuteInChunksAsync(
                operations,
                new BatchWriteOptions { DegreeOfParallelism = 4 },
                (chunk, ct) =>
                {
                    Interlocked.Increment(ref executionCount);
                    receivedValue = chunk[0];
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.That(executionCount, Is.EqualTo(1));
            Assert.That(receivedValue, Is.EqualTo(42));
        }

        [Test]
        public async Task ExecuteInChunksAsync_preserves_all_items_across_chunks()
        {
            var operations = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                operations.Add(i);
            }

            var allProcessed = new List<int>();

            await BatchWriteHelper.ExecuteInChunksAsync(
                operations,
                new BatchWriteOptions { DegreeOfParallelism = 7 },
                (chunk, ct) =>
                {
                    lock (allProcessed)
                    {
                        allProcessed.AddRange(chunk);
                    }
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            allProcessed.Sort();
            Assert.That(allProcessed, Is.EqualTo(operations));
        }
    }
}

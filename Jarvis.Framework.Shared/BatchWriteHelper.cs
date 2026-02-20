using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared
{
    /// <summary>
    /// Helper to split a list of write operations into chunks and execute
    /// them in parallel according to <see cref="BatchWriteOptions"/>.
    /// </summary>
    internal static class BatchWriteHelper
    {
        /// <summary>
        /// Splits <paramref name="operations"/> into chunks based on
        /// <paramref name="options"/> and executes each chunk via
        /// <paramref name="executeChunk"/> with the configured degree
        /// of parallelism.
        /// </summary>
        /// <typeparam name="T">The type of write operation model.</typeparam>
        /// <param name="operations">The full list of write operations.</param>
        /// <param name="options">Batch write configuration. If null, uses Default.</param>
        /// <param name="executeChunk">
        /// Delegate that receives a single chunk and the CancellationToken,
        /// and performs the actual write.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task ExecuteInChunksAsync<T>(
            List<T> operations,
            BatchWriteOptions options,
            Func<List<T>, CancellationToken, Task> executeChunk,
            CancellationToken cancellationToken)
        {
            options ??= BatchWriteOptions.Default;
            options.Validate();

            if (operations.Count == 0)
            {
                return;
            }

            // Fast path: single chunk, no overhead - identical to current behavior
            if (options.DegreeOfParallelism <= 1)
            {
                await executeChunk(operations, cancellationToken).ConfigureAwait(false);
                return;
            }

            // Split into chunks and execute in parallel
            var chunks = ChunkList(operations, options.DegreeOfParallelism);

            await Parallel.ForEachAsync(
                chunks,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = options.DegreeOfParallelism,
                    CancellationToken = cancellationToken,
                },
                async (chunk, ct) =>
                {
                    await executeChunk(chunk, ct).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Splits a list into N roughly equal chunks. If the list has fewer
        /// elements than <paramref name="numberOfChunks"/>, each element
        /// gets its own chunk.
        /// </summary>
        private static List<List<T>> ChunkList<T>(List<T> source, int numberOfChunks)
        {
            var result = new List<List<T>>();
            int totalCount = source.Count;
            int chunkSize = (int)Math.Ceiling((double)totalCount / numberOfChunks);

            for (int i = 0; i < totalCount; i += chunkSize)
            {
                int count = Math.Min(chunkSize, totalCount - i);
                result.Add(source.GetRange(i, count));
            }

            return result;
        }
    }
}

using System;

namespace Jarvis.Framework.Shared
{
    /// <summary>
    /// Configuration for controlling how batch write operations are executed
    /// against MongoDB. Allows splitting a single batch into multiple chunks
    /// and executing them in parallel for maximum throughput.
    /// </summary>
    public class BatchWriteOptions
    {
        /// <summary>
        /// Default instance with no parallelism. All operations execute as a single batch.
        /// </summary>
        public static BatchWriteOptions Default { get; } = new BatchWriteOptions();

        /// <summary>
        /// Controls how many parallel write operations to execute.
        /// The operation list is split into this many chunks, all executed concurrently.
        /// Default = 1 (single write, identical to non-parallel behavior).
        /// </summary>
        public int DegreeOfParallelism { get; init; } = 1;

        /// <summary>
        /// Validates the configuration and throws if invalid.
        /// </summary>
        internal void Validate()
        {
            if (DegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(DegreeOfParallelism),
                    DegreeOfParallelism,
                    "DegreeOfParallelism must be >= 1.");
            }
        }
    }
}

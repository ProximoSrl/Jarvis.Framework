using System;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// <para>
    /// A local, crash-safe medium that stores the last dispatched checkpoint token for each slot.
    /// It is the durability substitute for the (deferred) MongoDB write: the checkpoint tracker
    /// records every dispatched checkpoint here immediately and pushes to MongoDB only periodically.
    /// On startup the tracker reconciles the value found in MongoDB with the value found here,
    /// keeping whichever is greater, so a crash between two MongoDB flushes never causes an
    /// already-dispatched side effect to be replayed.
    /// </para>
    /// <para>
    /// The default implementation is <see cref="FileSystemCheckpointDurableStore"/>. A different
    /// implementation can be injected into <see cref="ConcurrentCheckpointTracker"/> (for example one
    /// that writes to a specific directory, or an in-memory store in tests) instead of overriding a
    /// folder path.
    /// </para>
    /// </summary>
    internal interface ICheckpointDurableStore : IDisposable
    {
        /// <summary>
        /// Returns the highest durable checkpoint token recorded for the slot, or 0 when the slot
        /// has never been recorded.
        /// </summary>
        long Read(string slotName);

        /// <summary>
        /// Records a checkpoint token for a slot. The value is monotonic: a token lower than or
        /// equal to the currently stored one is ignored. When <paramref name="flushToDisk"/> is
        /// <c>true</c> the write is forced to stable storage (fsync) before returning; this is used
        /// for dispatched commits whose side effects must never be replayed after a crash.
        /// </summary>
        void Record(string slotName, long checkpointToken, bool flushToDisk);

        /// <summary>
        /// Forces the durable token for a slot to exactly <paramref name="checkpointToken"/>,
        /// bypassing the monotonic guard. Used when a rebuild intentionally lowers/clears the
        /// checkpoint. The write is always flushed to stable storage.
        /// </summary>
        void Reset(string slotName, long checkpointToken);
    }
}

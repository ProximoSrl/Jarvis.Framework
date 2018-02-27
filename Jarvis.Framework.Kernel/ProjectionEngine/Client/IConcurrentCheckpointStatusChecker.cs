using System;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// An interface used to query the status of projection with
    /// the concurrent checkpoint tracker.
    /// </summary>
    public interface IConcurrentCheckpointStatusChecker
    {
        /// <summary>
        /// Returns true if all projection that are marked as active
        /// reached that specific checkpoint.
        /// </summary>
        /// <param name="checkpointToken">The checkpoint to check</param>
        /// <returns></returns>
        Task<Boolean> IsCheckpointProjectedByAllProjectionAsync(Int64 checkpointToken);
    }
}

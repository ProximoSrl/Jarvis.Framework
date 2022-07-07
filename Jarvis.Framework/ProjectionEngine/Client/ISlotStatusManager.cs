using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// A component that is used to know the status of all slots, 
    /// to determine if we have new projection, or if some of existing
    /// slot needs to be rebuilt.
    /// </summary>
    public interface ISlotStatusManager
    {
        /// <summary>
        /// Given the list of actual projection it returns slot status to allow the caller
        /// to decide what to do with new projection, etc.
        /// </summary>
        /// <returns></returns>
        CheckpointSlotStatus GetSlotsStatus();

        /// <summary>
        /// Return a list of errors if there are anomalies in projection
        /// checkpoints.
        /// It should be called after all projection are configured and 
        /// before start polling to dispatch commit.
        /// </summary>
        /// <returns></returns>
        IEnumerable<String> GetCheckpointErrors();

        /// <summary>
        /// Useful for new slots, to allow them to be rebuilded up the slot with
        /// maximum number of dispatched commits.
        /// </summary>
        /// <returns></returns>
        Int64 GetMaxDispatchedCheckpoint();

        /// <summary>
        /// Get a list of projection change information, for all projection that are changed.
        /// </summary>
        /// <returns></returns>
        IEnumerable<ProjectionChangeInfo> GetProjectionChangeInfo();

        /// <summary>
        /// Create all records for a slot where all the Values are set at
        /// <paramref name="valueCheckpointToken"/> value. This is used for 
        /// new slots where you want a full rebuild when the slot is introduced
        /// for the first time.
        /// </summary>
        /// <param name="slotName"></param>
        /// <param name="valueCheckpointToken"></param>
        void CreateSlotAtCheckpoint(string slotName, Int64 valueCheckpointToken);
    }
}

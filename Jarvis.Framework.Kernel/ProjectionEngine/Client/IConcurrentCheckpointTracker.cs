using System;
using Jarvis.Framework.Kernel.Events;
using System.Collections.Generic;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface IConcurrentCheckpointTracker
    {
        void SetCheckpoint(string id, Int64 value);
        
        CheckPointReplayStatus GetCheckpointStatus(string id, Int64 checkpoint);

        Int64 GetCheckpoint(IProjection projection);
        Int64 GetMinCheckpoint();

        /// <summary>
        /// Useful for new slots, to allow them to be rebuilded up the slot with
        /// maximum number of dispatched commits.
        /// </summary>
        /// <returns></returns>
        Int64 GetMaxDispatchedCheckpoint();

        void SetUp(IProjection[] projections, int version, Boolean setupMetrics);

        /// <summary>
        /// This is used to reset completely the CheckpointTracker, with this call it is possibile to 
        /// setup again everything.
        /// </summary>
        void Clear();

        bool NeedsRebuild(IProjection projection);

        void RebuildStarted(IProjection projection, Int64 lastCommit);
        void RebuildEnded(IProjection projection, ProjectionMetrics.Meter meter);
        void UpdateSlot(string slotName, Int64 checkpointToken);
        Int64 GetCurrent(IProjection projection);

        void UpdateSlotAndSetCheckpoint(
            string slotName, 
            IEnumerable<String> projectionIdList,
            Int64 valueCheckpointToken,
            Int64? currentCheckpointToken = null);

        /// <summary>
        /// Return a list of errors if there are anomalies in projection
        /// checkpoints.
        /// It should be called after all projection are configured and 
        /// before start polling to dispatch commit.
        /// </summary>
        /// <returns></returns>
        List<String> GetCheckpointErrors();

        /// <summary>
        /// Given the list of actual projection it returns slot status to allow the caller
        /// to decide what to do with new projection, etc.
        /// </summary>
        /// <param name="projections"></param>
        /// <returns></returns>
        CheckpointSlotStatus GetSlotsStatus(IProjection[] projections);
    }

    /// <summary>
    /// This class gives information on all the slots in the system, it is used to understand
    /// if some projection needs rebuild, if some projection is new or if there are some errors.
    /// </summary>
    public class CheckpointSlotStatus
    {
        /// <summary>
        /// This list contains all the slot that needs rebuild because one of the projection has
        /// a different signature than the one actually on database.
        /// </summary>
        public List<String> SlotsThatNeedsRebuild { get; private set; }

        /// <summary>
        /// This contains all the slots that are new, this is needed to allow the user to decide if this
        /// slot should start at 0 or it should start at different value. 
        /// 
        /// Usually new projection that does not have logic different from rebuild can be safely rebuild
        /// to use nitro.
        /// </summary>
        public List<String> NewSlots { get; private set; }

        public List<SlotError> SlotsWithErrors { get; private set; }

        public class SlotError
        {
            public String SlotName { get; set; }

            public String Errors { get; set; }
        }

        public CheckpointSlotStatus()
        {
            SlotsThatNeedsRebuild = new List<string>();
            NewSlots = new List<string>();
            SlotsWithErrors = new List<SlotError>();
        }
    }

}
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

        /// <summary>
        /// Update a whole slot and set both the Current and the Value.
        /// </summary>
        /// <param name="slotName"></param>
        /// <param name="projectionIdList"></param>
        /// <param name="valueCheckpointToken"></param>
        /// <param name="currentCheckpointToken"></param>
        void UpdateSlotAndSetCheckpoint(
            string slotName,
            IEnumerable<String> projectionIdList,
            Int64 valueCheckpointToken,
            Int64? currentCheckpointToken = null);
    }
}
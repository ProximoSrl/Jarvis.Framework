using System;
using Jarvis.Framework.Kernel.Events;
using System.Collections.Generic;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface IConcurrentCheckpointTracker
    {
        void SetCheckpoint(string id, string value);
        void LoadAll();
        CheckPointReplayStatus GetCheckpointStatus(string id, string checkpoint);
        void Clear();
        string GetCheckpoint(IProjection projection);
        Int64 GetMinCheckpoint();
        void SetUp(IProjection[] projections, int version);
        bool NeedsRebuild(IProjection projection);

        void RebuildStarted(IProjection projection, String lastCommit);
        void RebuildEnded(IProjection projection, ProjectionMetrics.Meter meter);
        void UpdateSlot(string slotName, string checkpointToken);
        string GetCurrent(IProjection projection);

        /// <summary>
        /// Return a list of errors if there are anomalies in projection
        /// checkpoints.
        /// It should be called after all projection are configured and 
        /// before start polling to dispatch commit.
        /// </summary>
        /// <returns></returns>
        List<String> GetCheckpointErrors();
    }
}
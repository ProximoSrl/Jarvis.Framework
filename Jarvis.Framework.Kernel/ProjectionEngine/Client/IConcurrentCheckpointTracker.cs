using System;
using Jarvis.Framework.Kernel.Events;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface IConcurrentCheckpointTracker
    {
        void SetCheckpoint(string id, string value);
        void LoadAll();
        CheckPointReplayStatus GetCheckpointStatus(string id, string checkpoint);
        void Clear();
        string GetCheckpoint(IProjection projection);
        void SetUp(IProjection[] projections, int version);
        bool NeedsRebuild(IProjection projection);

        void RebuildStarted(IProjection projection, String lastCommit);
        void RebuildEnded(IProjection projection, ProjectionMetrics.Meter meter);
        void UpdateSlot(string slotName, string checkpointToken);
        string GetCurrent(IProjection projection);
    }
}
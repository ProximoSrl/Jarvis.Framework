using Jarvis.Framework.Kernel.Events;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface IConcurrentCheckpointTracker
    {
        /// <summary>
        /// This is the time after the checkpoint in database is updated even if
        /// the slot did not dispatched any events for processed commit. We need to
        /// flush every X seconds because if a slot does not process any events, the
        /// checkpoint will never be updated and when the projection service restart
        /// it will re-dispatch lots of unnecessary events.
        /// </summary>
        /// <remarks>A negative or zero value actively disable deferred flush returning
        /// the checkpoint to the standard / classic operation mode.</remarks>
        int FlushNotDispatchedTimeoutInSeconds { get; set; }

        /// <summary>
        /// Given the name of a projection, it will return a CheckPointReplayStatus
        /// object that tells if this checkpoint was already dispatched (isrebuild).
        /// Useful only if we track progress of each projection.
        /// </summary>
        /// <param name="projectionName"></param>
        /// <param name="checkpoint"></param>
        /// <returns></returns>
        CheckPointReplayStatus GetCheckpointStatus(string projectionName, Int64 checkpoint);

        /// <summary>
        /// Get value property of checkpoint tracker
        /// </summary>
        /// <param name="projection"></param>
        /// <returns></returns>
        Int64 GetCheckpoint(IProjection projection);

        /// <summary>
        /// Get value of Current property of checkpoint tracker, useful for projection engine
        /// to know the minimum checkpoint to read when projection service restart.
        /// </summary>
        /// <param name="projection"></param>
        /// <returns></returns>
        Int64 GetCurrent(IProjection projection);

        /// <summary>
        /// Setup a projection to be tracked.
        /// </summary>
        /// <param name="projections"></param>
        /// <param name="version"></param>
        /// <param name="setupMetrics"></param>
        void SetUp(IProjection[] projections, int version, Boolean setupMetrics);

        void RebuildStarted(IProjection projection, Int64 lastCommit);

        void RebuildEnded(IProjection projection, ProjectionMetrics.Meter meter);

        /// <summary>
        /// Update a whole slot and set both the Current and the Value.
        /// </summary>
        /// <param name="slotName"></param>
        /// <param name="projectionNameList">List of the projections belonging to this slot</param>
        /// <param name="valueCheckpointToken"></param>
        /// <param name="someEventDispatched">If the chunk was dispatched, but not processed by the slot (not projection really
        /// used events of that chunk) there is no need to update the checkpoint every chunk. This parameters indicates
        /// if some of the events were processed or not to optimize the checkpoint.</param>
        Task UpdateSlotAndSetCheckpointAsync(
            string slotName,
            IEnumerable<String> projectionNameList,
            Int64 valueCheckpointToken,
            Boolean someEventDispatched);

        /// <summary>
        /// Called from standard projection engine to update both value and current
        /// field for a single projection.
        /// </summary>
        /// <param name="projectionName"></param>
        /// <param name="checkpointToken"></param>
        /// <returns></returns>
        Task UpdateProjectionCheckpointAsync(
            string projectionName,
            Int64 checkpointToken);

        /// <summary>
        /// If the caller needs checkpoint to be flushed on disk, you can periodically call this function.
        /// This is needed because if a slot does not dispatch any event, the checkpoint record is not
        /// updated until a certain amount of time passed and at least another event is dispatched.
        /// If the caller wants the checkpoint to be updated, they can optionally call this method to
        /// force flush the value.
        /// </summary>
        /// <returns></returns>
        Task FlushCheckpointAsync();
    }
}
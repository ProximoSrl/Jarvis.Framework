using System;
using Jarvis.Framework.Kernel.Events;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface IConcurrentCheckpointTracker
    {
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
		Task UpdateSlotAndSetCheckpointAsync(
            string slotName,
            IEnumerable<String> projectionNameList,
            Int64 valueCheckpointToken);

        /// <summary>
        /// Force a flush of the in memory checkpoints to the database collection.
        /// </summary>
        /// <returns></returns>
        Task FlushCheckpointCollectionAsync();
    }
}
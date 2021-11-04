using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Kernel.Support
{
    public interface IProjectionTargetCheckpointLoader
    {
        Int64 GetMaxCheckpointToDispatch();
    }

    public interface IProjectionStatusLoader
    {
        /// <summary>
        /// retrieve a set of metrics for slot that gives the idea
        /// of how many commit needs to be dispatched for each slot
        /// </summary>
        /// <returns></returns>
        IEnumerable<SlotStatus> GetSlotMetrics();

        /// <summary>
        /// Get status of a single slot name.
        /// </summary>
        /// <param name="slotName"></param>
        /// <returns></returns>
        SlotStatus GetSlotMetric(String slotName);
    }

    public class SlotStatus
    {
        public static readonly SlotStatus Null;

        static SlotStatus()
        {
            Null = new SlotStatus("", 0);
        }

        public String Name { get; private set; }

        public Int64 CommitBehind { get; private set; }

        public SlotStatus(string name, long commitBehind)
        {
            Name = name;
            CommitBehind = commitBehind;
        }
    }
}

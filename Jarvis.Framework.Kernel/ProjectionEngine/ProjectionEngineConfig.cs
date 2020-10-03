using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.MultitenantSupport;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class ProjectionEngineConfig
    {
        public int PollingMsInterval { get; set; }
        public string EventStoreConnectionString { get; set; }
        public string[] Slots { get; set; }
        public int ForcedGcSecondsInterval { get; set; }
        public TenantId TenantId { get; set; }

        public Int32 DelayedStartInMilliseconds { get; set; }

        public String EngineVersion { get; set; }

        public List<BucketInfo> BucketInfo { get; set; }

        public ProjectionEngineConfig()
        {
            PollingMsInterval = 100;
        }

        internal String Validate()
        {
            if (String.IsNullOrEmpty(this.EventStoreConnectionString))
                return $"Eventstore connection string is null";

            return null;
        }
    }

    public interface ITriggerProjectionsUpdate
    {
        Task UpdateAsync();
        Task UpdateAndWaitAsync();
    }
}
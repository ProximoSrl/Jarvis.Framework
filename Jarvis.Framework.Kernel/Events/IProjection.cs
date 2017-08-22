using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fasterflect;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.MultitenantSupport;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Events
{
    public interface IProjection
    {
        TenantId TenantId { get; }

        Task DropAsync();

        Task SetUpAsync();

        Task<Boolean> HandleAsync(IDomainEvent e, bool isReplay);

        Task StartRebuildAsync(IRebuildContext context);

        Task StopRebuildAsync();

        void Observe(IObserveProjection observer);

        bool IsRebuilding { get; }

        /// <summary>
        /// Gives me the priority of the Projection. at Higher numbers correspond
        /// higher priority
        /// </summary>
        Int32 Priority { get;  }

        ProjectionInfoAttribute Info { get; }

        /// <summary>
        /// Called when the projection engine dispatched all the events of a checkpoint.
        /// </summary>
        /// <param name="checkpointToken"></param>
        void CheckpointProjected(Int64 checkpointToken);
    }
}

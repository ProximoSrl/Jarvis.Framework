﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fasterflect;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.MultitenantSupport;

namespace Jarvis.Framework.Kernel.Events
{
    public interface IProjection
    {
        TenantId TenantId { get; }

        void Drop();

        void SetUp();

        bool Handle(IDomainEvent e, bool isReplay);

        void StartRebuild(IRebuildContext context);

        void StopRebuild();

        void Observe(IObserveProjection observer);

        bool IsRebuilding { get; }

        /// <summary>
        /// Gives me the priority of the Projection. at Higher numbers correspond
        /// higher priority
        /// </summary>
        Int32 Priority { get;  }

        ProjectionInfoAttribute Info { get; }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fasterflect;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.MultitenantSupport;

namespace Jarvis.Framework.Kernel.Events
{
    public interface IObserveProjection
    {
        void RebuildStarted();
        void RebuildEnded();
    }
}

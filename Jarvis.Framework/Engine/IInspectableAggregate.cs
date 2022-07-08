using Jarvis.Framework.Shared.Events;
using System;

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// An inspectable aggregate is an aggregate that tells to the 
    /// caller when an event is raised, this is necessary to implement some
    /// infrastructure logic like composite commands.
    /// </summary>
    public interface IInspectableAggregate
    {
        /// <summary>
        /// Add an action inspector that will be called whenever an event is
        /// raised.
        /// </summary>
        /// <param name="inspector"></param>
        void SetInspectorOnRaiseEvent(Action<DomainEvent> inspector);
    }
}

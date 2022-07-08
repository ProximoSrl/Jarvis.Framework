using NStore.Core.Snapshots;
using System;

namespace Jarvis.Framework.Kernel.Engine
{
    public interface IEntityRoot : ISnapshottable
    {
        /// <summary>
        /// Id of entity
        /// </summary>
        String Id { get; }

        /// <summary>
        /// Function to init the entity.
        /// </summary>
        /// <param name="raiseEventFunction"></param>
        /// <param name="ownerId"></param>
        void Init(Action<object> raiseEventFunction, String ownerId);

        /// <summary>
        /// We need to access the state of the entity root because
        /// we need to apply events to it.
        /// </summary>
        /// <returns></returns>
        JarvisEntityState GetState();

        /// <summary>
        /// This is called by owner aggregate to signal to the entity that the
        /// father Aggregate is about to raise an event. This allows the aggregate
        /// to do some custom logic.
        /// </summary>
        /// <param name="event"></param>
        void EventEmitting(object @event);
    }
}

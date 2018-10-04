using System;
using System.Linq;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
    [AttributeUsage(AttributeTargets.Class)]
    public class AtomicReadmodelInfoAttribute : Attribute
    {
        public static AtomicReadmodelInfoAttribute GetFrom(Type readmodelType)
        {
            return readmodelType
                .GetCustomAttributes(true)
                .OfType<AtomicReadmodelInfoAttribute>()
                .SingleOrDefault();
        }

        public AtomicReadmodelInfoAttribute(String name, Type aggregateIdType)
        {
            Name = name;
            AggregateIdType = aggregateIdType;
        }

        /// <summary>
        /// This is the name of the readmodel, used by the checkpoint tracker.
        /// </summary>
        public String Name { get; private set; }

        /// <summary>
        /// For each atomic readmodel I can project only the events
        /// that comes from a specific aggregate, this information is crucial
        /// for performance.
        /// <ol>
        /// <li>I can query events database only for Chunks where partition id
        /// starts with the name of the id</li>
        /// <li>I can avoid dispatching unnecessary events to readmodel, if I 
        /// already have loaded a chunk.</li>
        /// </ol>
        /// </summary>
        public Type AggregateIdType { get; set; }
    }
}

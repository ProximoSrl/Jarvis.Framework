using Jarvis.Framework.Shared.Events;
using NStore.Domain;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Kernel.Engine
{
    public static class StaticUpcaster
    {
        /// <summary>
        /// <para>
        /// Not concurrent because you register everything before doing real work, so we are sure
        /// that all writing is done before someone starts reading.
        /// </para>
        /// <para>
        /// This dictionary has the upcasted event as the key, and the upcaster as the value so you can
        /// take an event that needs to be upcasted and quickly find the upcaster if present.
        /// </para>
        /// </summary>
        private static readonly Dictionary<Type, IUpcaster> _upcasters = new Dictionary<Type, IUpcaster>();

        /// <summary>
        /// <para>
        /// This dictionary is needed to know the upcasting chain from the last event. This is needed because
        /// when we inspect projection to understand what kind of events the projection needs we need to take
        /// into account not only the real event handled, but  all the events that can be upcasted to that event.
        /// </para>
        /// <para>Key is the destination event and value is the event that can be upcasted to the destination event.</para>
        /// </summary>
        private static readonly Dictionary<Type, List<Type>> _reverseUpcasterChain = new Dictionary<Type, List<Type>>();

        public static object UpcastChangeset(object cs)
        {
            if (_upcasters.Count > 0 && cs is Changeset changeset)
            {
                for (int i = 0; i < changeset.Events.Length; i++)
                {
                    if (changeset.Events[i] is DomainEvent evt)
                    {
                        changeset.Events[i] = UpcastEvent(evt);
                    }
                }
            }

            return cs;
        }

        public static DomainEvent UpcastEvent(DomainEvent evt)
        {
            while (_upcasters.TryGetValue(evt.GetType(), out var upcaster))
            {
                evt = upcaster.GenericUpcast(evt);
            }
            return evt;
        }

        internal static void Clear()
        {
            _upcasters.Clear();
        }

        internal static void RegisterUpcaster(IUpcaster upcaster)
        {
            var upcastType = upcaster.UpcastedEventType;
            if (_upcasters.TryGetValue(upcastType, out var existingUpcaster))
            {
                throw new ArgumentException($"Cannot register upcaster {upcaster.GetType().FullName} because another upcaster already exists for type {upcaster.UpcastedEventType} implemneted by class {existingUpcaster.GetType().FullName}");
            }
            _upcasters[upcastType] = upcaster;

            //now register reverse upcaster.
            var destinationType = upcaster.DestinationEventType;
            if (!_reverseUpcasterChain.TryGetValue(destinationType, out var chain))
            {
                chain = new List<Type>();
                _reverseUpcasterChain[destinationType] = chain;
            }
            chain.Add(upcastType);
        }

        /// <summary>
        /// Given a source event this will found all the events that can be upcasted to 
        /// the event send as parameter.
        /// </summary>
        public static IEnumerable<Type> FindUpcastedEvents(Type type)
        {
            Queue<Type> events = new Queue<Type>();
            events.Enqueue(type);
            HashSet<Type> returnedEvents = new HashSet<Type>();
            while (events.Count > 0)
            {
                //take on event and try to upcast.
                Type dequeued = events.Dequeue();
                if (_reverseUpcasterChain.TryGetValue(dequeued, out var list))
                {
                    foreach (var item in list)
                    {
                        if (!returnedEvents.Contains(item))
                        {
                            yield return item;
                            events.Enqueue(item);
                            returnedEvents.Add(item);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Upcaster is able to "upcast" an event to another event.
    /// </summary>
    internal interface IUpcaster
    {
        /// <summary>
        /// This is the event source, the event that needs to be upcasted.
        /// </summary>
        Type UpcastedEventType { get; }

        /// <summary>
        /// We need to know also the destination of the conversion, to create a full 
        /// chain of upcasters.
        /// </summary>
        Type DestinationEventType { get; }

        /// <summary>
        /// Generic interface upcaster
        /// </summary>
        /// <param name="domainEvent"></param>
        /// <returns></returns>
        DomainEvent GenericUpcast(DomainEvent domainEvent);
    }

    public abstract class BaseUpcaster<T, N> : IUpcaster
        where T : DomainEvent where N : DomainEvent
    {
        public Type UpcastedEventType => typeof(T);

        public Type DestinationEventType => typeof(N);

        public N Upcast(T eventToUpcast)
        {
            N upcasted = OnUpcast(eventToUpcast);

            //remember taht some property of the event is handled internally and are not persisted
            upcasted.AggregateId = eventToUpcast.AggregateId;
            upcasted.CommitId = eventToUpcast.CommitId;
            upcasted.Version = eventToUpcast.Version;
            upcasted.Context = eventToUpcast.Context;
            upcasted.CheckpointToken = eventToUpcast.CheckpointToken;

            return upcasted;
        }

        public DomainEvent GenericUpcast(DomainEvent domainEvent)
        {
            return Upcast((T)domainEvent);
        }

        protected abstract N OnUpcast(T eventToUpcast);
    }
}

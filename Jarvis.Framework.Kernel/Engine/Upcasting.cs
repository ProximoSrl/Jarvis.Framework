using Jarvis.Framework.Shared.Events;
using NStore.Domain;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Kernel.Engine
{
    public static class StaticUpcaster
    {
        /// <summary>
        /// Not concurrent because you register everything before doing real work, so we are sure
        /// that all writing is done before someone starts reading.
        /// </summary>
        private static readonly Dictionary<Type, IUpcaster> _upcasters = new Dictionary<Type, IUpcaster>();

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
        }
    }

    internal interface IUpcaster
    {
        Type UpcastedEventType { get; }

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

        public N Upcast(T eventToUpcast)
        {
            N upcasted = OnUpcast(eventToUpcast);

            //remember taht some property of the event is handled internally and are not persisted
            upcasted.AggregateId = eventToUpcast.AggregateId;
            upcasted.CommitId = eventToUpcast.CommitId;
            upcasted.CommitStamp = eventToUpcast.CommitStamp;
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

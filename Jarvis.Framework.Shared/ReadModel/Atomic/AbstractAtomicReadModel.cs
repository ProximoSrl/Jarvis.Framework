using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using NStore.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
    /// <summary>
    /// An abstract atomic readmodel is a readmodel that is 
    /// capable to dispatch messages to give a view of a specific
    /// aggregate. It is made to process only events of a single
    /// aggregate.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
#pragma warning disable S1210 // "Equals" and the comparison operators should be overridden when implementing "IComparable"
    public abstract class AbstractAtomicReadModel :
#pragma warning restore S1210 // "Equals" and the comparison operators should be overridden when implementing "IComparable"
        IAtomicReadModel
    {
        protected AbstractAtomicReadModel(String id)
        {
            Id = id;
        }

        /// <summary>
        /// Id of the aggregate
        /// </summary>
        public String Id { get; private set; }

        /// <summary>
        /// This is the Position of the last chunk. All <see cref="NStore.Domain.Changeset"/> are projected
        /// all events in a row. We do not save readmodel until all events of changeset are processed.
        /// </summary>
		public Int64 ProjectedPosition { get; private set; }

        public Boolean Faulted { get; private set; }

        public void MarkAsFaulted(Int64 projectedPosition)
        {
            ProjectedPosition = projectedPosition;
            Faulted = true;
        }

        private Int32? _readModelVersion;

        /// <summary>
        /// This is a read / write property because we want this property
        /// to be serialized in mongo and retrieved to check if the readmodel
        /// in mongo has a different signature. Lower signature is the sign 
        /// of an older readmodel. Higher signature is the sign of an higher 
        /// and newer readmodel.
        /// </summary>
        public Int32 ReadModelVersion
        {
            get
            {
                return (_readModelVersion ?? (_readModelVersion = GetVersion())).Value;
            }
            set
            {
                _readModelVersion = value;
            }
        }

        protected abstract Int32 GetVersion();

        private Boolean ProcessEvent(DomainEvent domainEvent)
        {
            //if this readmodel is faulted, it should not process anymore any data.
            if (Faulted)
                return false;

            //remember that we have a method for each combination ReadmodelType+EventType
            var eventType = domainEvent.GetType();
            string key = eventType.FullName + GetType().FullName;

            if (!_handlersCache.TryGetValue(key, out MethodInvoker invoker))
            {
                var methodInfo = GetType().Method("On", new Type[] { eventType }, Flags.InstancePublic | Flags.InstancePrivate);
                if (methodInfo != null)
                {
                    invoker = methodInfo.DelegateForCallMethod();
                }
                _handlersCache.TryAdd(key, invoker);
            }

            if (invoker != null)
            {
                invoker.Invoke(this, domainEvent);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Process the changeset, returing true if the <see cref="Changeset"/> contains at least
        /// one event that is handled by the readmodel.
        /// </summary>
        /// <param name="changeset"></param>
        /// <returns></returns>
        public virtual Boolean ProcessChangeset(Changeset changeset)
        {
            Boolean processed = false;
            if (changeset.Events.Length > 0 && changeset.GetChunkPosition() > ProjectedPosition)
            {
                Int64 position = 0;
                foreach (DomainEvent evt in changeset.Events)
                {
                    processed |= ProcessEvent(evt);
                    position = evt.CheckpointToken;
                }
                ProjectedPosition = position;
            }
            return processed;
        }

        private static ConcurrentDictionary<string, MethodInvoker> _handlersCache = new ConcurrentDictionary<string, MethodInvoker>();
    }
}
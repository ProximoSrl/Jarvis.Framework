using System.Collections;
using System.Collections.Generic;

namespace Jarvis.NEventStoreEx.CommonDomainEx
{
    public interface IAggregateEx
    {
        IIdentity Id { get; }
        int Version { get; }

        /// <summary>
        /// If zero this entity was not restored by a snapshot, if different from 
        /// zero this instance is restored by snapshot.
        /// </summary>
        int SnapshotRestoreVersion { get; }

        void ApplyEvent(object @event);
        ICollection GetUncommittedEvents();
        void ClearUncommittedEvents();

        IMementoEx GetSnapshot();
        void EnterContext(IDictionary<string, object> context);
    }
}
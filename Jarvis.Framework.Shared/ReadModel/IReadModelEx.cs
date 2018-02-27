using Jarvis.Framework.Shared.Events;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IReadModelEx<TKey> : IReadModel
    {
        TKey Id { get; set; }
        int Version { get; set; }

        DateTime LastModified { get; set; }

        /// <summary>
        /// Return true if the specific event was already processed by the readmodel.
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
		bool BuiltFromEvent(DomainEvent evt);

        /// <summary>
        /// It sets the last event projected on this readmodel, it uses both the commit
        /// position and event position to generate a unique index numer
        /// </summary>
        /// <param name="commitPosition"></param>
        /// <param name="eventPosition"></param>
        void SetEventProjected(Int64 commitPosition, Int32 eventPosition);

        void ThrowIfInvalidId();
    }

    public interface IPollableReadModel
    {
        /// <summary>
        /// This is useful when you index this object in ES, so you can
        /// undersand the commit used to generate this value.
        /// </summary>
        Int64 CheckpointToken { get; set; }

        /// <summary>
        /// This is an unique value used to distinguish when 
        /// <see cref="CheckpointToken"/> is the same for two records.
        /// </summary>
        Int64 SecondaryToken { get; set; }

        /// <summary>
        /// When a readmodel used for polling is deleted, it should
        /// be marked as deleted and the record should not really
        /// be deleted from the storage.
        /// </summary>
        Boolean Deleted { get; set; }
    }

    public interface ITopicsProvider
    {
        IEnumerable<string> GetTopics();
    }
}
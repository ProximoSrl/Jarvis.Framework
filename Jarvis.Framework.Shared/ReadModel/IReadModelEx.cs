using Jarvis.Framework.Shared.Events;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IReadModelEx<TKey> : IReadModel
    {
        TKey Id { get; set; }

        /// <summary>
        /// This version is a number that is incremented each time the 
        /// readmodel is changed, this ABSOLUTELY NOT REFLECT THE REAL VERSION
        /// OF THE AGGREGATE. It is only an indication on how many time the
        /// aggregate was written.
        /// </summary>
        int Version { get; set; }

        /// <summary>
        /// This is the aggregate version, it will represent the real version
        /// of the aggregate, and it is equal to the number of COMMITS NOT EVENTS
        /// RAISED ON THAT AGGREGATE
        /// </summary>
        long AggregateVersion { get; set; }

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
        /// <param name="evt"></param>
        void SetEventProjected(DomainEvent evt);

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
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.ReadModel
{
    public interface IReadModelEx<TKey> : IReadModel
    {
        TKey Id { get; set; }
        int Version { get; set; }

        DateTime LastModified { get; set; }

        bool BuiltFromEvent(Guid id);
        void AddEvent(Guid id);

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
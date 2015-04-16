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

    public interface ITopicsProvider
    {
        IEnumerable<string> GetTopics();
    }
}
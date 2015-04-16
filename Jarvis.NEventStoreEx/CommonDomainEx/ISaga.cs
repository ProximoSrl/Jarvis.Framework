using System.Collections;
using System.Collections.Generic;

namespace Jarvis.NEventStoreEx.CommonDomainEx
{
    public interface ISagaEx
    {
        string Id { get; }

        int Version { get; }

        void Transition(object message);

        ICollection<object> GetUncommittedEvents();

        void ClearUncommittedEvents();

        ICollection<object> GetUndispatchedMessages();

        void ClearUndispatchedMessages();

        void SetReplayMode(bool replayOn);
    }
}
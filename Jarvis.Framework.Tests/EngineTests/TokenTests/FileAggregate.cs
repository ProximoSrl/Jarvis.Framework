using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;

namespace Jarvis.Framework.Tests.EngineTests.TokenTests
{
    public class FileId : EventStoreIdentity
    {
        public FileId(long id) : base(id)
        {
        }

        public FileId(string id) : base(id)
        {
        }
    }

    public class FileAggregate : AggregateRoot<FileAggregateState>
    {
        public FileAggregate()
        {
        }

        public void Lock()
        {
            if(!InternalState.IsLocked)
                RaiseEvent(new FileLocked());
        }

        public void UnLock()
        {
            if (InternalState.IsLocked)
                RaiseEvent(new FileUnLocked());
        }
    }

    public class FileLocked : DomainEvent
    {
    }

    public class FileUnLocked : DomainEvent
    {
    }
}
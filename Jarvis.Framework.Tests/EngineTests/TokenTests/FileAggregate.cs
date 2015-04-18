using System;
using System.Collections.Generic;
using System.Linq;
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

    public class FileLockGrant : GrantName
    {
        public FileLockGrant()
            : base("file-lock")
        {
        }
    }

    public class FileAggregate : AggregateRoot<FileAggregateState>
    {
        public static readonly FileLockGrant LockGrant = new FileLockGrant();

        public void Lock(Token lockToken)
        {
            ThrowIfAlreadyGranted(LockGrant);

            var grant = CreateGrant(LockGrant, lockToken);
            if (grant == null)
                return;

            RaiseEvent(new FileLocked(grant));
        }

        public void UnLock()
        {
            var grant = RequireGrant(LockGrant);

            RaiseEvent(new FileUnLocked(grant));
            RevokeGrant(grant);
        }
    }



    public class FileLocked : DomainEvent
    {
        public Grant LockGrant { get; private set; }

        public FileLocked(Grant lockGrant)
        {
            LockGrant = lockGrant;
        }
    }

    public class FileUnLocked : DomainEvent
    {
        public Grant UnlockGrant { get; private set; }

        public FileUnLocked(Grant unlockGrant)
        {
            UnlockGrant = unlockGrant;
        }
    }
}
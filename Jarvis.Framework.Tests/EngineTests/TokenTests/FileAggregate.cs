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

    public class FileAggregate : AggregateRoot<FileAggregateState>
    {
        public FileAggregate()
        {
            this.ExecutionGrants = new HashSet<Grant>();
        }

        private HashSet<Grant> ExecutionGrants { get; set; }

        public void Lock()
        {
            if(!InternalState.IsLocked)
                RaiseEvent(new FileLocked());
        }

        public void UnLock()
        {
            RequireGrant(new GrantName("lock"));

            if (InternalState.IsLocked)
                RaiseEvent(new FileUnLocked());
        }

        private void RequireGrant(GrantName grant)
        {
            if (ExecutionGrants.All(x => x.GrantName != grant))
                throw new MissingGrantException(grant);
        }

        public void AddContextGrant(GrantName grantName, Token token)
        {
            this.ExecutionGrants.Add(new Grant(token, grantName));
        }
    }

    internal class MissingGrantException : Exception
    {
        public MissingGrantException(GrantName grant)
        {
            
        }
    }

    public class FileLocked : DomainEvent
    {
    }

    public class FileUnLocked : DomainEvent
    {
    }
}
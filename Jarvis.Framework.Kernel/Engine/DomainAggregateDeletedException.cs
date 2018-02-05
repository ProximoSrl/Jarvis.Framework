using System;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.IdentitySupport;

namespace Jarvis.Framework.Kernel.Engine
{
    public class DomainAggregateDeletedException : DomainException
    {
        public DomainAggregateDeletedException(IIdentity id, Type t)
            : base(id, t.Name)
        {
        }
    }
}
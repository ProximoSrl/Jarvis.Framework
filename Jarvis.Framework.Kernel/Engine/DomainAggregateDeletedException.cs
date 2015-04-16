using System;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

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
using System;
using Jarvis.Framework.Shared.MultitenantSupport;

namespace Jarvis.Framework.Kernel.MultitenantSupport.Exceptions
{
    public class TenantAlreadyPresentException : Exception
    {
        public TenantId TenantId { get; private set; }

        public TenantAlreadyPresentException(TenantId tenantId)
        {
            TenantId = tenantId;
        }
    }
}

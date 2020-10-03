using Jarvis.Framework.Shared.MultitenantSupport;
using System;

#if NETFULL
using System.Runtime.Remoting.Messaging;

namespace Jarvis.Framework.Kernel.MultitenantSupport
{
    public static class TenantContext
    {
        private const string TenantIdKey = "tenant_id";

        public static void Enter(TenantId id)
        {
            CallContext.LogicalSetData(TenantIdKey, id);
        }

        public static TenantId CurrentTenantId
        {
            get
            {
                return (TenantId)CallContext.LogicalGetData(TenantIdKey);
            }
        }
        public static void Exit()
        {
            CallContext.LogicalSetData(TenantIdKey, null);
        }
    }
}
#else
namespace Jarvis.Framework.Kernel.MultitenantSupport
{
    public static class TenantContext
    {
        private const string TenantIdKey = "tenant_id";

        public static void Enter(TenantId id)
        {
            throw new NotSupportedException("Not supported in .NET standard");
        }

        public static TenantId CurrentTenantId
        {
            get
            {
                throw new NotSupportedException("Not supported in .NET standard");
            }
        }

        public static void Exit()
        {
            throw new NotSupportedException("Not supported in .NET standard");
        }
    }
}
#endif

using Jarvis.Framework.Shared.MultitenantSupport;

#if NETSTANDARD
using CurrentCallContext = Jarvis.Framework.Shared.Support.NetCoreCallContext;
#else
using CurrentCallContext = System.Runtime.Remoting.Messaging.CallContext;
#endif

namespace Jarvis.Framework.Kernel.MultitenantSupport
{
    public static class TenantContext
    {
        private const string TenantIdKey = "tenant_id";

        public static void Enter(TenantId id)
        {
            CurrentCallContext.LogicalSetData(TenantIdKey, id);
        }

        public static TenantId CurrentTenantId
        {
            get
            {
                return (TenantId)CurrentCallContext.LogicalGetData(TenantIdKey);
            }
        }
        public static void Exit()
        {
            CurrentCallContext.LogicalSetData(TenantIdKey, null);
        }
    }
}


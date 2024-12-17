using Jarvis.Framework.Shared.MultitenantSupport;
using CurrentCallContext = Jarvis.Framework.Shared.Support.NetCoreCallContext;

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


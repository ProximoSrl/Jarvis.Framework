using System.Runtime.Remoting.Messaging;
using Jarvis.Framework.Shared.MultitenantSupport;

namespace Jarvis.Framework.Kernel.MultitenantSupport
{
    public static class TenantContext
    {
        private const string TenantIdKey = "tenant_id";

        public static void Enter(TenantId id)
        {
            CallContext.LogicalSetData(TenantIdKey, id);       
        }

        public static TenantId CurrentTenantId {
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

using System;
using Jarvis.Framework.Shared.MultitenantSupport;
using System.Security.Permissions;
using System.Runtime.Serialization;

namespace Jarvis.Framework.Kernel.MultitenantSupport.Exceptions
{
    /// <summary>
    /// https://stackoverflow.com/questions/94488/what-is-the-correct-way-to-make-a-custom-net-exception-serializable
    /// </summary>
    [Serializable]
    public class TenantAlreadyPresentException : Exception
    {
        private readonly TenantId tenantId;

        public TenantId TenantId => tenantId;

        public TenantAlreadyPresentException(TenantId tenantId)
        {
            this.tenantId = tenantId;
        }

        public TenantAlreadyPresentException() : base()
        {
        }

        public TenantAlreadyPresentException(string message) : base(message)
        {
        }

        public TenantAlreadyPresentException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Do not forget to correctly handle the serialization info in the exception.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected TenantAlreadyPresentException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.tenantId = new TenantId(info.GetString("TenantId"));
        }

        /// <summary>
        /// https://stackoverflow.com/questions/94488/what-is-the-correct-way-to-make-a-custom-net-exception-serializable
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            info.AddValue("TenantId", this.tenantId.ToString());

            // MUST call through to the base class to let it save its own state
            base.GetObjectData(info, context);
        }
    }
}

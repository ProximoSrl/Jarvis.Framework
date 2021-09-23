using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// Exception raised when a command is executed with
    /// <see cref="MessagesConstants.IfVersionEqualsTo"/> header active
    /// and the aggregate is newer than requested version.
    /// </summary>
    [Serializable]
    public class AggregateModifiedException : DomainException
    {
        public Int64 VersionRequested { get; private set; }

        public Int64 ActualVersion { get; private set; }

        public AggregateModifiedException(string message, String aggregateId, Int64 versionRequested, Int64 actualVersion) : base(aggregateId, message)
        {
            VersionRequested = versionRequested;
            ActualVersion = actualVersion;
        }

        protected AggregateModifiedException(String aggregateId, SerializationInfo info, StreamingContext context) : base(aggregateId, info, context)
        {
        }

        protected AggregateModifiedException(string message) : base(message)
        {
        }

        public AggregateModifiedException() : base()
        {
        }

        public AggregateModifiedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public AggregateModifiedException(IIdentity id) : base(id)
        {
        }

        public AggregateModifiedException(IIdentity id, string message) : base(id, message)
        {
        }

        public AggregateModifiedException(string id, string message) : base(id, message)
        {
        }

        public AggregateModifiedException(string id, string message, Exception innerException) : base(id, message, innerException)
        {
        }

        /// <summary>
        /// Do not forget to correctly handle the serialization info in the exception.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected AggregateModifiedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.ActualVersion = info.GetInt64("ActualVersion");
            this.VersionRequested = info.GetInt64("VersionRequested");
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

            info.AddValue("ActualVersion", this.ActualVersion);
            info.AddValue("VersionRequested", this.VersionRequested);

            // MUST call through to the base class to let it save its own state
            base.GetObjectData(info, context);
        }
    }
}

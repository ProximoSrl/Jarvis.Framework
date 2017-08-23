using System;
using Jarvis.Framework.Shared.Messages;
using System.Runtime.Serialization;
using System.Security.Permissions;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.IdentitySupport;

namespace Jarvis.Framework.Kernel.Engine
{
	/// <summary>
	/// This is the exception raised when a command synced from offline
	/// mode and violates the checkpoint rule <see cref="MessagesConstants.SessionStartCheckEnabled"/>
	/// </summary>
	[Serializable]
    public class AggregateSyncConflictException : DomainException
    {
        public Int64 CheckpointToken { get; private set; }

        public String SessionGuidId { get; private set; }

        public AggregateSyncConflictException(
            string message,
            String aggregateId,
            Int64 checkpointToken,
            String sessionGuidId) : base(aggregateId, message)
        {
            CheckpointToken = checkpointToken;
            SessionGuidId = sessionGuidId;
        }

        protected AggregateSyncConflictException(String aggregateId, SerializationInfo info, StreamingContext context) : base(aggregateId, info, context)
        {
        }

        protected AggregateSyncConflictException(string message) : base(message)
        {
        }

        public AggregateSyncConflictException() : base()
        {
        }

        public AggregateSyncConflictException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public AggregateSyncConflictException(IIdentity id) : base(id)
        {
        }

        public AggregateSyncConflictException(IIdentity id, string message) : base(id, message)
        {
        }

        public AggregateSyncConflictException(string id, string message) : base(id, message)
        {
        }

        public AggregateSyncConflictException(string id, string message, Exception innerException) : base(id, message, innerException)
        {
        }

        /// <summary>
        /// Do not forget to correctly handle the serialization info in the exception.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected AggregateSyncConflictException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.CheckpointToken = info.GetInt64("CheckpointToken");
            this.SessionGuidId = info.GetString("SessionGuidId");
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

            info.AddValue("CheckpointToken", this.CheckpointToken);
            info.AddValue("SessionGuidId", this.SessionGuidId);

            // MUST call through to the base class to let it save its own state
            base.GetObjectData(info, context);
        }
    }
}

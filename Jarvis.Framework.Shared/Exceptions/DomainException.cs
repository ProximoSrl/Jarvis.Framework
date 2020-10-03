using Jarvis.Framework.Shared.IdentitySupport;
using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Jarvis.Framework.Shared.Exceptions
{
    [Serializable]
    public class DomainException : Exception
    {
        public string AggregateId { get; protected set; }

        protected DomainException(string message)
            : base(message)
        {
        }

        public DomainException() : base()
        {
        }

        public DomainException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public DomainException(IIdentity id)
        {
            this.AggregateId = id.AsString();
        }

        public DomainException(IIdentity id, string message)
            : this(id != null ? id.ToString() : "", message)
        {
        }

        public DomainException(string id, string message)
            : base(message)
        {
            this.AggregateId = id;
        }

        public DomainException(string id, string message, Exception innerException)
            : base(message, innerException)
        {
            this.AggregateId = id;
        }

        protected DomainException(string id, SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.AggregateId = id;
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        protected DomainException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.AggregateId = info.GetString("aggregateId");
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            info.AddValue("aggregateId", this.AggregateId);

            // MUST call through to the base class to let it save its own state
            base.GetObjectData(info, context);
        }
    }
}
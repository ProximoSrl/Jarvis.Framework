using System;
using System.Runtime.Serialization;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Core
{
    public class DomainException : Exception
    {
        public string AggregateId { get; protected set; }

        protected DomainException(string message)
            : base(message)
        {
        }

        public DomainException(IIdentity id)
        {
            this.AggregateId = id.AsString();
        }

        public DomainException(IIdentity id, string message)
            :this(id != null ? id.ToString() : "", message)
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
    }
}
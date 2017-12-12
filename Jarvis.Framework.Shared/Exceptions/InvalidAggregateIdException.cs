using Jarvis.Framework.Shared.Exceptions;
using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Jarvis.Framework.Shared.Exceptions
{
	public class InvalidAggregateIdException : DomainException
	{
        public InvalidAggregateIdException() : base("Identificativo non valido")
        {
        }

		public InvalidAggregateIdException(String reason): base(reason)
		{
		}

		protected InvalidAggregateIdException(string id, SerializationInfo info, StreamingContext context)
		   : base(info, context)
		{
			this.AggregateId = id;
		}

		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		protected InvalidAggregateIdException(SerializationInfo info, StreamingContext context) : base(info, context)
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
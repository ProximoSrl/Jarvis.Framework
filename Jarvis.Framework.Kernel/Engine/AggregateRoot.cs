using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Domain;

namespace Jarvis.Framework.Kernel.Engine
{
	/// <summary>
	/// classe base per gli aggregati che gestiscono lo stato tramite un <c>AggregateState</c>
	/// </summary>
	/// <typeparam name="TState">Tipo dello stato</typeparam>
	/// <typeparam name="TId"></typeparam>
	public abstract class AggregateRoot<TState, TId> : Aggregate<TState>, IInvariantsChecker
		where TState : JarvisAggregateState, new()
		where TId : EventStoreIdentity
	{
		protected TState InternalState => State;

		protected AggregateRoot() : base(AggregateStateEventPayloadProcessor.Instance)
		{
		}

		private TId _jarvisId;

		protected TId GetJarvisId()
		{
			return _jarvisId ?? (_jarvisId = (TId)Activator.CreateInstance(typeof(TId), this.Id));
		}

		protected void RaiseEvent(object @event)
		{
			var domainEvent = @event as DomainEvent;
			if (domainEvent == null)
				throw new JarvisFrameworkEngineException("Raised Events should inherits from DomainEvent class, invalid event type: " + @event.GetType().FullName);
			domainEvent.SetPropertyValue(d => d.AggregateId, GetJarvisId());
			base.Emit(@event);
		}

		public bool HasBeenCreated
		{
			get
			{
				return this.Version > 0 || this.IsDirty;
			}
		}

		public virtual InvariantsCheckResult CheckInvariants()
		{
			return State.CheckInvariants();
		}

		protected void ThrowDomainException(string format, params object[] p)
		{
			if (p.Length > 0)
			{
				throw new DomainException(Id, string.Format(format, p));
			}
			throw new DomainException(Id, format);
		}

		public bool IsValid
		{
			get
			{
				return this.Version > 0;
			}
		}
	}
}

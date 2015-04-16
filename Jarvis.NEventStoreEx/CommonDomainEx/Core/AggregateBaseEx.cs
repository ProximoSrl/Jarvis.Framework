using System;
using System.Collections;
using System.Collections.Generic;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Core
{
    public abstract class AggregateBaseEx : IAggregateEx, IEquatable<IAggregateEx>
	{
		private readonly ICollection<object> uncommittedEvents = new LinkedList<object>();

		private IRouteEventsEx registeredRoutes;

		protected AggregateBaseEx()
			: this(null)
		{ }

		protected AggregateBaseEx(IRouteEventsEx handler)
		{
			if (handler == null)
			{
				return;
			}

			this.RegisteredRoutes = handler;
			this.RegisteredRoutes.Register(this);
		}

		protected IRouteEventsEx RegisteredRoutes
		{
			get
			{
				return this.registeredRoutes ?? (this.registeredRoutes = new ConventionEventRouterEx(true, this));
			}
			set
			{
				if (value == null)
				{
					throw new InvalidOperationException("AggregateBase must have an event router to function");
				}

				this.registeredRoutes = value;
			}
		}

		public IIdentity Id { get; protected set; }
		public int Version { get; protected set; }

		void IAggregateEx.ApplyEvent(object @event)
		{
			this.RegisteredRoutes.Dispatch(@event);
			this.Version++;
		}

		ICollection IAggregateEx.GetUncommittedEvents()
		{
			return (ICollection)this.uncommittedEvents;
		}

		void IAggregateEx.ClearUncommittedEvents()
		{
			this.uncommittedEvents.Clear();
		}

		IMementoEx IAggregateEx.GetSnapshot()
		{
			IMementoEx snapshot = this.GetSnapshot();
			snapshot.Id = this.Id;
			snapshot.Version = this.Version;
			return snapshot;
		}

		public virtual bool Equals(IAggregateEx other)
		{
			return null != other && other.Id == this.Id;
		}

		protected void Register<T>(Action<T> route)
		{
			this.RegisteredRoutes.Register(route);
		}

		protected void RaiseEvent(object @event)
		{
			((IAggregateEx)this).ApplyEvent(@event);
			this.uncommittedEvents.Add(@event);
		}

		protected virtual IMementoEx GetSnapshot()
		{
			return null;
		}

		public override int GetHashCode()
		{
			return this.Id.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return this.Equals(obj as IAggregateEx);
		}
	}
}
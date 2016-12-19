using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Events;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;
using Jarvis.Framework.Shared.Helpers;
namespace Jarvis.Framework.Kernel.Engine
{
    public abstract class AggregateRoot : AggregateBaseEx
    {
        private ILogger _logger = NullLogger.Instance;
        public ILogger Logger
        {
            get { return _logger; }
            set { _logger = value; }
        }

        /// <summary>
        /// indica se l'aggregate ha stato (almeno 1 evento)
        /// </summary>
        public bool IsValid
        {
            get
            {
                return this.Version > 0;
            }
        }

        /// <summary>
        /// Verifica se un aggregato è valido (ha eventi) e scatena una eccezione in caso negativo
        /// </summary>
        /// <exception cref="DomainException"></exception>
        public void CheckValid()
        {
            if (!IsValid)
                throw new InvalidDomainException(this);
        }

        internal AggregateRoot(IRouteEventsEx handler)
            : base(handler)
        {
        }

        public void AssignAggregateId(IIdentity id)
        {
            if (Id != null)
            {
                throw new InvalidAggregateOperationException(string.Format(
                    "{0} id already assigned {1}. Cannot assign {2}",
                    GetType().FullName, Id, id
                ));
            }

            if (id == null)
                throw new InvalidAggregateIdException();

            this.Id = id;
        }
    }

    /// <summary>
    /// classe base per gli aggregati che gestiscono lo stato tramite un <c>AggregateState</c>
    /// </summary>
    /// <typeparam name="TState">Tipo dello stato</typeparam>
    public abstract class AggregateRoot<TState> : AggregateRoot, ISnapshotable, IInvariantsChecker
        where TState : AggregateState, new()
    {

        private TState _internalState;
        protected TState InternalState
        {
            get { return _internalState; }
        }

        protected AggregateRoot()
            : base(new AggregateRootEventRouter<TState>())
        {
            ((AggregateRootEventRouter<TState>)RegisteredRoutes).AttachAggregateRoot(this);
            _internalState = new TState();
        }

        protected override void RaiseEvent(object @event)
        {
            var domainEvent = @event as DomainEvent;
            if (domainEvent == null)
                throw new ApplicationException("Raised Events should inherits from DomainEvent class, invalid event type: " + @event.GetType().FullName);
            domainEvent.SetPropertyValue(d => d.AggregateId, Id);
            base.RaiseEvent(@event);
        }

        protected override IMementoEx GetSnapshot()
        {
            return Snapshot();
        }

        IMementoEx ISnapshotable.GetSnapshot()
        {
            return Snapshot();
        }

        public AggregateSnapshot<TState> Snapshot()
        {
            //to avoid any problem with multithread or external references, whenever
            //the aggregate root creates a snapshot it cloned the state to avoid
            //external code reference internal state.
            return new AggregateSnapshot<TState>
            {
                Id = this.Id,
                Version = this.Version,
                State = (TState) _internalState.Clone()
            };
        }

        public void Apply(DomainEvent evt)
        {
            InternalState.Apply(evt);
        }

        Boolean ISnapshotable.Restore(IMementoEx snapshot)
        {
            var snap = (AggregateSnapshot<TState>)snapshot;

			if (snap.State.Signature != _internalState.Signature)
			{
				//This version of the state is not compatibile.
				return false;
			}

            //it is important to clone to avoid external code reference internal state
            this._internalState = (TState) snap.State.Clone(); 
            this.SnapshotRestoreVersion = this.Version = snap.Version;
            
            if (!Id.Equals(snap.Id))
                throw new AggregateException(String.Format("Error restoring snapshot: Id mismatch. Snapshot id: {0} aggregate id: {1} [snapshot version {2}] ", 
                    snap.Id, Id, snap.Version));

			return true;
        }


        public bool HasBeenCreated
        {
            get
            {
                return this.Version > 0;
            }
        }

        public InvariantCheckResult CheckInvariants()
        {
            return _internalState.CheckInvariants();
        }

        protected void ThrowDomainException(string format, params object[] p)
        {
            if (p.Any())
            {
                throw new DomainException(Id, string.Format(format, p));
            }
            throw new DomainException(Id, format);
        }


    }
}

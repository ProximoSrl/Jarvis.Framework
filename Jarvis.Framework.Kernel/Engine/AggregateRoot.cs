using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

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
        private HashSet<Grant> ExecutionGrants { get; set; }


        protected TState InternalState
        {
            get { return _internalState; }
        }

        protected AggregateRoot()
            : base(new AggregateRootEventRouter<TState>())
        {
            ((AggregateRootEventRouter<TState>)RegisteredRoutes).AttachAggregateRoot(this);
            _internalState = new TState();
            ExecutionGrants = new HashSet<Grant>();
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
            return new AggregateSnapshot<TState>
            {
                Id = this.Id,
                Version = this.Version,
                State = _internalState
            };
        }

        public void Apply(DomainEvent evt)
        {
            InternalState.Apply(evt);
        }

        void ISnapshotable.Restore(IMementoEx snapshot)
        {
            var snap = (AggregateSnapshot<TState>)snapshot;
            this._internalState = snap.State;
            this.Version = snap.Version;
            if (!Id.Equals(snap.Id))
                throw new AggregateException(String.Format("Error restoring snapshot: Id mismatch. Snapshot id: {0} aggregate id: {1} [snapshot version {2}] ", 
                    snap.Id, Id, snap.Version));
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

        protected Grant RequireGrant(GrantName grantName)
        {
            var grant = ExecutionGrants.SingleOrDefault(x => x.GrantName == grantName);
            if(grant == null || !InternalState.ValidateGrant(grant))
                throw new MissingGrantException(grantName);

            return grant;
        }

        protected void ThrowIfAlreadyGranted(GrantName name)
        {
            if(InternalState.HasGrant(name))
                throw new GrantViolationException(name);
        }

        protected Grant CreateGrant(GrantName name, Token token)
        {
            var grant = new Grant(token, name);
            if (ExecutionGrants.Contains(grant))
            {
                return null;
            }

            return grant;
        }

        public void AddContextGrant(GrantName grantName, Token token)
        {
            this.ExecutionGrants.Add(new Grant(token, grantName));
        }
    }

    public class MissingGrantException : Exception
    {
        public MissingGrantException(GrantName grant)
        {

        }
    }   
    
    public class GrantViolationException : Exception
    {
        public GrantViolationException(GrantName grant)
        {

        }
    }
}
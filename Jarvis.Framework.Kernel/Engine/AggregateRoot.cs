using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Domain;
using NStore.Core.Processing;
using NStore.Core.Snapshots;

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

		protected IPayloadProcessor payloadProcessor;

		protected AggregateRoot() : base(AggregateStateEventPayloadProcessor.Instance)
		{
			payloadProcessor = AggregateStateEventPayloadProcessor.Instance;
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
            BeforeEmitEvent(@event);
			base.Emit(@event);
			//Now dispatch event to all entities
			foreach (var childStates in InternalState.EntityStates)
			{
				payloadProcessor.Process(childStates.Value, @event);
			}
		}

        private void BeforeEmitEvent(object @event)
        {
            //Give the entityes the ability to do something before emitting events.
            if (childEntities?.Count > 0)
            {
                foreach (var entity in childEntities.Values)
                {
                    entity.EventEmitting(@event);
                }
            }
            OnBeforeEmitEvent(@event);
        }

        protected virtual void OnBeforeEmitEvent(object @event)
        {
            //Do nothing, let concrete class do something if they want to.
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

		protected override string StateSignature => InternalState.VersionSignature;

		public bool IsValid
		{
			get
			{
				return this.Version > 0;
			}
		}

		protected SnapshotInfo RestoreSnapshot { get; private set; }
		protected Boolean WasRestoredFromSnapshot => RestoreSnapshot != null;

		private List<IEntityRoot> snapshotRestoredEntities;

		protected override SnapshotInfo PreprocessSnapshot(SnapshotInfo snapshotInfo)
		{
			var baseProcessing = base.PreprocessSnapshot(snapshotInfo);
			if (baseProcessing == null)
				return null;

			//We are trying to restore a snapshot that has not the very same version of itnernal state of this aggregate
			var emptyState = new TState();
			if (snapshotInfo.SchemaVersion != emptyState.VersionSignature)
				return null;

			JarvisAggregateState state = snapshotInfo.Payload as JarvisAggregateState;
			if (state == null)
				return null;

			//If we need to restore the aggregate we should restore all the entities
			snapshotRestoredEntities = new List<IEntityRoot>();
			foreach (var entity in CreateChildEntities(snapshotInfo.SourceId))
			{
				JarvisEntityState entityState;
				if (!state.EntityStates.TryGetValue(entity.Id, out entityState))
					return null; // one of the entity cannot be restored

				//TODO: Need to change nstore to avoid this.
				var info = new SnapshotInfo(snapshotInfo.SourceId, snapshotInfo.SourceVersion, entityState.Clone(), entityState.VersionSignature);
				if (!entity.TryRestore(info))
					return null; //This snapshot is not valid for the entity

				snapshotRestoredEntities.Add(entity);
			}

			//Set restore snapshot and create a new payload with a clone of the state
			RestoreSnapshot = snapshotInfo;
			return new SnapshotInfo(snapshotInfo.SourceId, snapshotInfo.SourceVersion, state.Clone(), snapshotInfo.SchemaVersion);
		}

#pragma warning disable S2743 // Static fields should not be used in generic types
		private static readonly IEntityRoot[] emptyListOfEntities = new IEntityRoot[0];
#pragma warning restore S2743 // Static fields should not be used in generic types

		/// <summary>
		/// If an aggregate want to create child entities it should override this function.
		/// </summary>
		/// <param name="aggregateId">Needed because during a snapshot restore</param>
		/// <returns></returns>
		protected virtual IEnumerable<IEntityRoot> CreateChildEntities(String aggregateId)
		{
			return emptyListOfEntities;
		}

		protected override void AfterInit()
		{
			base.AfterInit();

			//after we inited everything, we need to create entities if we are not restored from snapshot
			if (!WasRestoredFromSnapshot)
			{
				foreach (var entity in CreateChildEntities(Id))
				{
					AddEntityToAggregate(entity);
				}
			}
			else
			{
				//if we reach here entites were already restored from a snapshot, we simply need to add to the aggregate
				foreach (var entity in snapshotRestoredEntities)
				{
					AddEntityToAggregate(entity);
				}
			}
		}

		#region entity management

		private readonly Dictionary<String, IEntityRoot> childEntities = new Dictionary<String, IEntityRoot>();

		private void AddEntityToAggregate(IEntityRoot entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			entity.Init(RaiseEvent, Id);
			childEntities.Add(entity.Id, entity);

			//Entity states could already be restored from snapshot.
			if (!State.EntityStates.ContainsKey(entity.Id))
			{
				State.EntityStates.Add(entity.Id, entity.GetState());
			}
		}

		#endregion
	}
}

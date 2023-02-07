using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Bson.Serialization.Attributes;
using NStore.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
	/// <summary>
	/// An abstract atomic readmodel is a readmodel that is
	/// capable to dispatch messages to give a view of a specific
	/// aggregate. It is made to process only events of a single
	/// aggregate.
	/// </summary>
	public abstract class AbstractAtomicReadModel : IAtomicReadModel
	{
		/// <summary>
		/// Default constructor that will accept the id
		/// </summary>
		/// <param name="id"></param>
		protected AbstractAtomicReadModel(String id)
		{
			Id = id;
		}

		/// <summary>
		/// <para>
		/// This is a shared function used to grab UserId from a <see cref="DomainEvent"/>, it can
		/// be changed statically by caller. It is static because there is no need to have a different
		/// criteria for every readmodel.
		/// </para>
		/// <para>
		/// Basic implementation will use the <see cref="DomainEvent.IssuedBy"/> property and also
		/// will remove domain name from the value.
		/// </para>
		/// </summary>
		public static Func<DomainEvent, String> UserNameExtractor { get; set; } = InnerUserNameExtractor;

		private static string InnerUserNameExtractor(DomainEvent evt)
		{
			if (evt.IssuedBy != null)
			{
				if (evt.IssuedBy.Contains("\\"))
				{
					return evt.IssuedBy.Split('\\').Last();
				}

				return evt.IssuedBy;
			}
			return null;
		}

		/// <summary>
		/// Id of the aggregate
		/// </summary>
		public String Id { get; private set; }

		/// <summary>
		/// This is the Position of the last chunk. All <see cref="NStore.Domain.Changeset"/> are projected
		/// all events in a row. We do not save readmodel until all events of changeset are processed.
		/// </summary>
		public Int64 ProjectedPosition { get; private set; }

		/// <summary>
		/// User that created the object, this will be the user of the first <see cref="Changeset"/>
		/// </summary>
		public String CreationUser { get; set; }

		/// <summary>
		/// The user that generates last <see cref="Changeset"/>
		/// </summary>
		public String LastModificationUser { get; set; }

		/// <summary>
		/// Last modification timestamp
		/// </summary>
		public DateTime LastModify { get; set; }

		/// <summary>
		/// Maximum number of version to keep in <see cref="LastProcessedVersions"/> we do not need to store
		/// all versions, because honestly we do not expect to have anomalies so big that you can miss an event
		/// and then re-dispatch after dispatched other 20 events.
		/// </summary>
		public const int MaxNumberOfVersionToKeep = 20;

		/// <summary>
		/// To avoid creating error in projection where we have version 2 and 3 and for some reason we
		/// dispatch version 3 before the 2, we keep a list of all version processed. This is done because
		/// we can admit holes, so if the aggregate is in version X we can tolerate processing X+2 version
		/// but if later X+1 is processed we have a problem and need to throw signaling that the aggregate
		/// is faulted due to a wrong ordering.
		/// </summary>
		public List<long> LastProcessedVersions { get; private set; } = new List<long>();

		/// <summary>
		/// Version of the aggregate
		/// </summary>
		public Int64 AggregateVersion { get; private set; }

		/// <inheritdoc/>
		public Boolean Faulted { get; private set; }

		/// <inheritdoc/>
		public int FaultRetryCount { get; private set; }

		/// <inheritdoc/>
		public void MarkAsFaulted(Int64 projectedPosition)
		{
			ProjectedPosition = projectedPosition;
			FaultRetryCount = 0;
			Faulted = true;
		}

		private Int32? _readModelVersion;

		/// <summary>
		/// This is a read / write proper-ty because we want this property
		/// to be serialized in mongo and retrieved to check if the readmodel
		/// in mongo has a different signature. Lower signature is the sign
		/// of an older readmodel. Higher signature is the sign of an higher
		/// and newer readmodel.
		/// </summary>
		public Int32 ReadModelVersion
		{
			get
			{
				return (_readModelVersion ?? (_readModelVersion = GetVersion())).Value;
			}
			set
			{
				_readModelVersion = value;
			}
		}

		/// <summary>
		/// <para>
		/// If this property is true, it means that the readmodel was manipulated
		/// in memory with events that comes from other streams (draftable) or simply
		/// with in memory generated events.
		/// </para>
		/// <para>The stream should not be persisted.</para>
		/// </summary>
		[BsonIgnore]
		public Boolean ModifiedWithExtraStreamEvents { get; protected set; }

		/// <inheritdoc/>
		protected abstract Int32 GetVersion();

		/// <inheritdoc/>
		protected virtual void BeforeEventProcessing(DomainEvent domainEvent)
		{
		}

		/// <inheritdoc/>
		protected virtual void AfterEventProcessing(DomainEvent domainEvent)
		{
		}

		private Boolean ProcessEvent(DomainEvent domainEvent)
		{
			//if this readmodel is faulted, it should not process anymore any data.
			if (Faulted)
			{
				return false;
			}

			//remember that we have a method for each combination ReadmodelType+EventType
			var eventType = domainEvent.GetType();
			string key = eventType.FullName + GetType().FullName;

			if (!_handlersCache.TryGetValue(key, out MethodInvoker invoker))
			{
				var methodInfo = GetType().Method("On", new Type[] { eventType }, Flags.InstancePublic | Flags.InstancePrivate);
				if (methodInfo != null)
				{
					invoker = methodInfo.DelegateForCallMethod();
				}
				_handlersCache.TryAdd(key, invoker);
			}

			if (invoker != null)
			{
				BeforeEventProcessing(domainEvent);
				invoker.Invoke(this, domainEvent);
				AfterEventProcessing(domainEvent);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Process the changeset, returing true if the <see cref="Changeset"/> contains at least
		/// one event that is handled by the readmodel.
		/// </summary>
		/// <param name="changeset"></param>
		/// <returns></returns>
		public virtual Boolean ProcessChangeset(Changeset changeset)
		{
			Boolean processed = false;

			//We should process events only if the changes really HAS events and if this is not a changes of the past.
			if (changeset.AggregateVersion < this.AggregateVersion && !LastProcessedVersions.Contains(changeset.AggregateVersion))
			{
				//ok we have old changeset that was not dispatched, but this will generate exception if it is not so old
				//we keep track of MaxNumberOfVersionToKeep in a special array to keep track of holes in last MaxNumberOfVersionToKeep
				//Versions, for older version we cannot throw we can only skip.
				if (changeset.AggregateVersion > this.AggregateVersion - MaxNumberOfVersionToKeep)
				{
					throw new JarvisFrameworkEngineException($"WRONG PROCESS ORDER FOR ATOMIC READMODEL: Readmodel {Id} we are dispatching checkpoint {changeset.GetChunkPosition()} that has version {changeset.AggregateVersion} less than actual ReadmodelVersion that is at version {this.AggregateVersion} and version {changeset.AggregateVersion} was never dispatched in the past.");
				}
			}

			//now understand if this event must be processed for idempotency.
			bool shouldProcessEvents = changeset.Events.Length > 0 && changeset.AggregateVersion > AggregateVersion;
			if (shouldProcessEvents)
			{
				Int64 position = 0;
				for (int i = 0; i < changeset.Events.Length; i++)
				{
					var evt = changeset.Events[i] as DomainEvent;
					if (i == 0)
					{
						//First event, we need to set some standard informations. These informations
						//are equal for all the events of this specific changeset. 

						var id = evt.AggregateId;
						if (this.Id != id)
						{
							throw new JarvisFrameworkEngineException($"We are trying to project an event of aggregate {evt.AggregateId} on a readmodel with id {this.Id}");
						}
						LastModify = evt.CommitStamp;
						LastModificationUser = UserNameExtractor(evt) ?? evt.IssuedBy;
						AggregateVersion = changeset.AggregateVersion;
						if (CreationUser == null)
						{
							CreationUser = evt.IssuedBy;
						}
					}

					processed |= ProcessEvent(evt);
					position = evt.CheckpointToken;
				}
				ProjectedPosition = position;
			}
			AddLastProcessedVersion(changeset.AggregateVersion);
			return processed;
		}

		private void AddLastProcessedVersion(long aggregateVersion)
		{
			if (LastProcessedVersions == null) LastProcessedVersions = new List<long>();
			if (LastProcessedVersions.Count > MaxNumberOfVersionToKeep - 1)
			{
				LastProcessedVersions.RemoveAt(0);
			}
			LastProcessedVersions.Add(aggregateVersion);
		}

		/// <inheritdoc/>
		public void ProcessExtraStreamChangeset(Changeset changeset)
		{
			foreach (DomainEvent evt in changeset.Events.OfType<DomainEvent>())
			{
				ProcessEvent(evt);
			}
			ModifiedWithExtraStreamEvents = true;
		}

		private static readonly ConcurrentDictionary<string, MethodInvoker> _handlersCache = new ConcurrentDictionary<string, MethodInvoker>();
	}
}
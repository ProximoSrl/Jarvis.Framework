using System;
using System.Collections.Generic;
using System.Linq;
using NEventStore.Domain;
using NEventStore.Domain.Persistence;
using NEventStore;
using NEventStore.Persistence;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using Metrics;
using Jarvis.NEventStoreEx.Support;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
	public class AbstractRepository : IRepositoryEx
	{
		private const string AggregateTypeHeader = "AggregateType";

		private static readonly Counter RepositoryLoadCounter = Metric.Counter("RepositoryGetById", Unit.Custom("ticks"));
		private static readonly Counter FailedLockCounter = Metric.Counter("AbstractRepositoryFailedLocks", Unit.Calls);

		private readonly IDetectConflicts _conflictDetector;

		private readonly IStoreEvents _eventStore;

		private readonly IConstructAggregatesEx _factory;
		private readonly IIdentityConverter _identityConverter;

		private Boolean _disposed;
		public Boolean Disposed { get { return _disposed; } }

		/// <summary>
		/// When a stream is opened it is stored in dictionary 
		/// to avoid re-open on save.
		/// </summary>
		private readonly IDictionary<IAggregateEx, IEventStream> _streams =
			new Dictionary<IAggregateEx, IEventStream>();

		/// <summary>
		/// This is used to keep a better track of locking, because each unique instance of repository
		/// will have its unique index, and each lock is marked with that index.
		/// </summary>
		private readonly Int64 _identity;

		/// <summary>
		/// This is the last identity assigned.
		/// </summary>
		private static Int64 _lastAssignedIdentity;

		/// <summary>
		/// This hashset contains the identities this instance of repository had obtained an exclusive lock to. 
		/// The real instance that holds all locked entities is <see cref="IdSerializerDictionary"/> property.
		/// It is necessary to remove all the lock after this instance is disposed.
		/// </summary>
		private readonly HashSet<IIdentity> _identitiesLocked = new HashSet<IIdentity>();

		/// <summary>
		/// This dictionary is used to hold a list of the aggregate ids that are acutally locked.
		/// It is used to avoid two threads to access at the very same moment the same entity
		/// thus avoiding unnecessary ConcurrencyException.
		/// </summary>
		private static readonly ConcurrentDictionary<IIdentity, Int64> IdSerializerDictionary;

		public NEventStore.Logging.ILog _logger;

		static AbstractRepository()
		{
			IdSerializerDictionary = new ConcurrentDictionary<IIdentity, Int64>();
			_lastAssignedIdentity = 0;
		}

		protected AbstractRepository(
			IStoreEvents eventStore,
			IConstructAggregatesEx factory,
			IDetectConflicts conflictDetector,
			IIdentityConverter identityConverter,
			NEventStore.Logging.ILog logger)
		{
			this._eventStore = eventStore;
			this._factory = factory;
			this._conflictDetector = conflictDetector;
			this._identityConverter = identityConverter;
			this._identity = Interlocked.Increment(ref _lastAssignedIdentity);
			_logger = logger;
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public virtual TAggregate GetById<TAggregate>(IIdentity id) where TAggregate : class, IAggregateEx
		{
			return this.GetById<TAggregate>(Bucket.Default, id);
		}

		public virtual TAggregate GetById<TAggregate>(IIdentity id, int versionToLoad) where TAggregate : class, IAggregateEx
		{
			return this.GetById<TAggregate>(Bucket.Default, id, versionToLoad);
		}

		public TAggregate GetById<TAggregate>(string bucketId, IIdentity id) where TAggregate : class, IAggregateEx
		{
			return this.GetById<TAggregate>(bucketId, id, int.MaxValue);
		}

		public TAggregate GetById<TAggregate>(string bucketId, IIdentity id, int versionToLoad) where TAggregate : class, IAggregateEx
		{
			if (_disposed)
				throw new ObjectDisposedException("This instance of repository was already disposed");

			Stopwatch sw = null;
			if (NeventStoreExGlobalConfiguration.MetricsEnabled) sw = Stopwatch.StartNew();

			if (NeventStoreExGlobalConfiguration.RepositoryLockOnAggregateId)
			{
				//try to acquire a lock on the identity, to minimize risk of ConcurrencyException
				//do not sleep more than a certain amount of time (avoid some missing dispose).
				bool lockAcquired = TryAcquireLock(id);
				if (lockAcquired == false)
				{
					//There is nothing to do if a lock failed to be aquired, but it worth to be signaled.
					FailedLockCounter.Increment();
				}
			}

			ISnapshot snapshot = GetSnapshot<TAggregate>(bucketId, id, versionToLoad);
			IAggregateEx aggregate = GetAggregate<TAggregate>(id);
			snapshot = ApplyAndValidateSnapshot(aggregate, snapshot);

			IEventStream stream = OpenStream(bucketId, id, versionToLoad, snapshot);
			//cache loaded stream to avoid reload during save.
			_streams[aggregate] = stream;

			ApplyEventsToAggregate(versionToLoad, stream, aggregate);

			if (NeventStoreExGlobalConfiguration.MetricsEnabled)
			{
				sw.Stop();
				RepositoryLoadCounter.Increment(typeof(TAggregate).Name, sw.ElapsedTicks);
			}
			return aggregate as TAggregate;
		}


		/// <summary>
		/// Try to acquire the lock, if more than 5 second passed the lock is not acquired and the 
		/// method returns false.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		private bool TryAcquireLock(IIdentity id)
		{
			Int32 sleepCount = 0;
			Boolean lockAcquired = false;
			var stopCycle = 100 + NeventStoreExGlobalConfiguration.LockThreadSleepCount;
			while (!(lockAcquired = IdSerializerDictionary.TryAdd(id, _identity)) && sleepCount < stopCycle)
			{
				//some other thread is accessing that entity. Do a very fast spinning for a while, then sleep for a little.
				if (sleepCount < 100)
					Thread.SpinWait(100 * sleepCount);
				else
					Thread.Sleep(50);
				sleepCount++;
			}
			if (lockAcquired) _identitiesLocked.Add(id);
			return lockAcquired;
		}

		public virtual Int32 Save(IAggregateEx aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
		{
			return Save(Bucket.Default, aggregate, commitId, updateHeaders);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="bucketId"></param>
		/// <param name="aggregate"></param>
		/// <param name="commitId"></param>
		/// <param name="updateHeaders"></param>
		/// <returns></returns>
		public Int32 Save(string bucketId, IAggregateEx aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
		{
			if (_disposed)
				throw new ObjectDisposedException("This instance of repository was already disposed");

			Dictionary<string, object> headers = PrepareHeaders(aggregate, updateHeaders);
			Int32 uncommittedEvents = aggregate.GetUncommittedEvents().Count;
			while (true)
			{

				IEventStream stream = this.PrepareStream(bucketId, aggregate, headers);
				int commitEventCount = stream.CommittedEvents.Count;

				try
				{
					stream.CommitChanges(commitId);
					aggregate.ClearUncommittedEvents();
					return uncommittedEvents;
				}
				catch (DuplicateCommitException dex)
				{
					stream.ClearChanges();
					_logger.Debug(String.Format("Duplicate commit exception bucket {0} - id {1} - commitid {2}. \n{3}", bucketId, aggregate.Id, commitId, dex));
					return 0; //no events commited
				}
				catch (ConcurrencyException e)
				{
					_logger.Warn(String.Format("Concurrency Exception bucket {0} - id {1} - commitid {2}. \n{3}", bucketId, aggregate.Id, commitId, e));
					if (this.ThrowOnConflict(stream, commitEventCount))
					{
						//@@FIX -> aggiungere prima del lancio dell'eccezione
						stream.ClearChanges();
						throw new ConflictingCommandException(e.Message, e);
					}
					stream.ClearChanges();
				}
				catch (StorageException e)
				{
					throw new PersistenceException(e.Message, e);
				}
				finally
				{
					ReleaseAggregateId(aggregate.Id);
				}
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			_disposed = true;
			if (!disposing)
			{
				return;
			}
			//Clear all lock if not already done.
			if (_identitiesLocked.Count > 0)
			{
				foreach (var id in _identitiesLocked.ToList())
				{
					ReleaseAggregateId(id);
				}
			}
			foreach (var stream in this._streams)
			{
				stream.Value.Dispose();
			}

			this._streams.Clear();
		}

		private void ReleaseAggregateId(IIdentity id)
		{
			Int64 outTempValue;

			if (IdSerializerDictionary.TryGetValue(id, out outTempValue))
			{
				if (outTempValue == _identity)
				{
					IdSerializerDictionary.TryRemove(id, out outTempValue);
				}
			}
			if (_identitiesLocked.Contains(id))
			{
				_identitiesLocked.Remove(id);
			}
		}

		private static void ApplyEventsToAggregate(int versionToLoad, IEventStream stream, IAggregateEx aggregate)
		{
			if (versionToLoad == 0 || aggregate.Version < versionToLoad)
			{
				foreach (var @event in stream.CommittedEvents.Select(x => x.Body))
				{
					aggregate.ApplyEvent(@event);
				}
			}
		}

		private IAggregateEx GetAggregate<TAggregate>(IIdentity id)
		{
			return _factory.Build(typeof(TAggregate), id);
		}

		protected virtual ISnapshot GetSnapshot<TAggregate>(string bucketId, IIdentity id, int version)
		{
			var snapshot = this._eventStore.Advanced.GetSnapshot(bucketId, id.AsString(), version);
			return snapshot;
		}

		protected virtual ISnapshot ApplyAndValidateSnapshot(IAggregateEx aggregate, ISnapshot snapshot)
		{
			if (snapshot == null) return null;
			var applied = _factory.ApplySnapshot(aggregate, snapshot);
			if (!applied)
			{
				SnapshotDischarded(aggregate, snapshot);
				return null;
			}
			return snapshot;
		}

		protected virtual void SnapshotDischarded(IAggregateEx aggregate, ISnapshot snapshot) { }

		private IEventStream OpenStream(string bucketId, IIdentity id, int version, ISnapshot snapshot)
		{
			IEventStream stream;

			stream = snapshot == null ?
						   this._eventStore.OpenStream(bucketId, id.AsString(), 0, version)
						 : this._eventStore.OpenStream(snapshot, version);

			return stream;
		}
		private IEventStream PrepareStream(string bucketId, IAggregateEx aggregate, Dictionary<string, object> headers)
		{
			IEventStream stream;
			if (!this._streams.TryGetValue(aggregate, out stream))
			{
				this._streams[aggregate] = stream = this._eventStore.CreateStream(bucketId, aggregate.Id.AsString());
			}

			foreach (var item in headers)
			{
				stream.UncommittedHeaders[item.Key] = item.Value;
			}

			aggregate.GetUncommittedEvents()
					 .Cast<object>()
					 .Select(x => new EventMessage { Body = x })
					 .ToList()
					 .ForEach(stream.Add);

			return stream;
		}

		private static Dictionary<string, object> PrepareHeaders(
			IAggregateEx aggregate, Action<IDictionary<string, object>> updateHeaders)
		{
			var headers = new Dictionary<string, object>();

			headers[AggregateTypeHeader] = aggregate.GetType().FullName;
			if (updateHeaders != null)
			{
				updateHeaders(headers);
			}

			return headers;
		}

		private bool ThrowOnConflict(IEventStream stream, int skip)
		{
			IEnumerable<object> committed = stream.CommittedEvents.Skip(skip).Select(x => x.Body);
			IEnumerable<object> uncommitted = stream.UncommittedEvents.Select(x => x.Body);
			return this._conflictDetector.ConflictsWith(uncommitted, committed);
		}
	}
}
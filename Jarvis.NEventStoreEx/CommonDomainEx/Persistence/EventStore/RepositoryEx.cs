using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NEventStore.Domain;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;
using NEventStore;
using Jarvis.NEventStoreEx.Support;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
    public static class SnapshotsSettings
    {
        static readonly HashSet<Type> SnapshotOptOut = new HashSet<Type>();


        static SnapshotsSettings()
        {
        }

        public static void OptOut(Type type)
        {
            SnapshotOptOut.Add(type);
        }

        public static void ClearOptOut()
        {
            SnapshotOptOut.Clear();
        }

        public static bool HasOptedOut(Type type)
        {
            return SnapshotOptOut.Contains(type);
        }
    }

    public class RepositoryEx : AbstractRepository, IRepositoryEx
    {

        /// <summary>
        /// used to persist snapshots of aggregates.
        /// </summary>
        public ISnapshotManager SnapshotManager { get; set; }

        public RepositoryEx(
            IStoreEvents eventStore, 
            IConstructAggregatesEx factory, 
            IDetectConflicts conflictDetector, 
            IIdentityConverter identityConverter,
            NEventStore.Logging.ILog logger)
            : base(eventStore, factory, conflictDetector, identityConverter, logger)
        {
            SnapshotManager = NullSnapshotManager.Instance; //Default behavior is avoid snapshot entirely.
        }

        public override Int32 Save(IAggregateEx aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            var checker = aggregate as IInvariantsChecker;
            if (checker != null)
            {
                var result = checker.CheckInvariants();
                if (!result)
                {
                    throw new InvariantNotSatifiedException(aggregate.Id, result.ErrorMessage);
                }
            }

            var bucketId = GetBucketFor(aggregate);
            var numOfEvents = Save(bucketId, aggregate, commitId, updateHeaders);
            //If we reach here we need to check if we want to persiste a snapshot.
            SnapshotManager.Snapshot(aggregate, bucketId, numOfEvents);
            return numOfEvents;
        }

        public override TAggregate GetById<TAggregate>(IIdentity id, int versionToLoad)
        {
            return this.GetById<TAggregate>(GetBucketFor<TAggregate>(id), id, versionToLoad);
        }

        public override TAggregate GetById<TAggregate>(IIdentity id)
        {
            return this.GetById<TAggregate>(GetBucketFor<TAggregate>(id), id, int.MaxValue);
        }

        string GetBucketFor(IAggregateEx aggregate)
        {
            return GetBucketFor(aggregate.GetType(), aggregate.Id);
        }

        protected virtual string GetBucketFor(Type aggregateType, IIdentity id)
        {
            var bucketAttrib =
                 (BucketAttribute)aggregateType.GetCustomAttributes(typeof(BucketAttribute), true).FirstOrDefault();

            if (bucketAttrib != null)
            {
                return bucketAttrib.BucketId;
            }

            var ns = aggregateType.Namespace;
            var dot = ns.IndexOf('.');
            return dot > 0 ? ns.Substring(0, dot) : ns;
        }

        string GetBucketFor<TAggregate>(IIdentity id)
        {
            var aggregateType = typeof(TAggregate);
            return GetBucketFor(aggregateType, id);
        }

        protected override ISnapshot GetSnapshot<TAggregate>(string bucketId, IIdentity id, int version)
        {
            if (SnapshotsSettings.HasOptedOut(typeof(TAggregate)))
                return null;

            return SnapshotManager.Load(id.AsString(), version, typeof(TAggregate));
        }
    }
}

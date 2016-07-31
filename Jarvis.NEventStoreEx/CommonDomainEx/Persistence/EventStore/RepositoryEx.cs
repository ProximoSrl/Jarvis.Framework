using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CommonDomain;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;
using NEventStore;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore
{
    public static class SnapshotsSettings
    {
        static readonly HashSet<Type> SnapshotOptOut = new HashSet<Type>();
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

        public RepositoryEx(IStoreEvents eventStore, IConstructAggregatesEx factory, IDetectConflicts conflictDetector, IIdentityConverter identityConverter)
            : base(eventStore, factory, conflictDetector, identityConverter)
        {
        }

        public override void Save(IAggregateEx aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
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
            var domainRuleChecker = aggregate as IDomainRulesChecker;
            if (domainRuleChecker != null)
            {
                domainRuleChecker.CheckRules();
            }

            Save(GetBucketFor(aggregate), aggregate, commitId, updateHeaders);
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

            return base.GetSnapshot<TAggregate>(bucketId, id, version);
        }

    }
}

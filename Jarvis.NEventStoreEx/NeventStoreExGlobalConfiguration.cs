﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;

namespace Jarvis.NEventStoreEx
{
    public static class NeventStoreExGlobalConfiguration
    {
        internal static Boolean MetricsEnabled { get; private set; }

        /// <summary>
        /// When this parameter is true the <see cref="AbstractRepository"/> will try to 
        /// serialize the access to aggregate to avoid unnecessary ConcurrencyException when 
        /// multiple threads are executing commands that call a very same aggregate.
        /// </summary>
        internal static Boolean RepositoryLockOnAggregateId { get; private set; }

        /// <summary>
        /// When the <see cref="AbstractRepository"/> or the <see cref="AggregateCachedRepositoryFactory"/> 
        /// are trying to acquire lock it does a first fast SpinWait with the
        /// <see cref="Thread.SpinWait"/> method, then it start sleeping for a given number of time, waiting
        /// for the other thread to free. This value is the maximum number of sleep before the lock failed to 
        /// be acquired.
        /// </summary>
        internal static Int32 LockThreadSleepCount { get; private set; }

        static NeventStoreExGlobalConfiguration()
        {
            MetricsEnabled = true;
            RepositoryLockOnAggregateId = true;
            LockThreadSleepCount = 0;
        }

        public static void DisableMetrics()
        {
            MetricsEnabled = false;
        }

        public static void EnableMetrics()
        {
            MetricsEnabled = false;
        }

        public static void DisableRepositoryLockOnAggregateId()
        {
            RepositoryLockOnAggregateId = false;
        }

        public static void EnableRepositoryLockOnAggregateId()
        {
            RepositoryLockOnAggregateId = false;
        }

        public static void SetLockThreadSleepCount(Int32 lockThreadSleepCount)
        {
            LockThreadSleepCount = lockThreadSleepCount;
        }

    }
}

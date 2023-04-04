using System;
using System.Threading;

namespace Jarvis.Framework.Shared
{
    public static class NeventStoreExGlobalConfiguration
    {
        internal static Boolean MetricsEnabled { get; private set; }

        /// <summary>
        /// When the <see cref="NStore.Domain.IRepository"/> or the <see cref="NStore.Domain.IRepository"/> 
        /// are trying to acquire lock it does a first fast SpinWait with the
        /// <see cref="Thread.SpinWait"/> method, then it start sleeping for a given number of time, waiting
        /// for the other thread to free. This value is the maximum number of sleep before the lock failed to 
        /// be acquired.
        /// </summary>
        internal static Int32 LockThreadSleepCount { get; private set; }

        static NeventStoreExGlobalConfiguration()
        {
            MetricsEnabled = true;
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

        public static void SetLockThreadSleepCount(Int32 lockThreadSleepCount)
        {
            LockThreadSleepCount = lockThreadSleepCount;
        }
    }
}

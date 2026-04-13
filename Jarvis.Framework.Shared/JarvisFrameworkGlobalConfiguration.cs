using System;
using System.Runtime.CompilerServices;

namespace Jarvis.Framework.Shared
{
    public static class JarvisFrameworkGlobalConfiguration
    {
        public static Boolean OfflineEventsReadmodelIdempotencyCheck { get; private set; }

        public static Boolean AtomicProjectionEngineOptimizedCatchup { get; private set; }

        /// <summary>
        /// We really do not need to store in jarvis-log.messages events or notifications.
        /// </summary>
        public static Boolean DisableJarvisLogForNonCommandType { get; private set; } = true;

        /// <summary>
        /// Due to an anomaly in the driver.
        /// </summary>
        public static Boolean MongoDbAsyncDisabled { get; private set; }

        static JarvisFrameworkGlobalConfiguration()
        {
            AtomicProjectionEngineOptimizedCatchup = true;
        }

        public static void DisableMongoDbAsync()
        {
            MongoDbAsyncDisabled = true;
        }

        public static void EnableOfflineEventsReadmodelIdempotencyCheck()
        {
            OfflineEventsReadmodelIdempotencyCheck = true;
        }

        public static void DisableOfflineEventsReadmodelIdempotencyCheck()
        {
            OfflineEventsReadmodelIdempotencyCheck = false;
        }

        public static void EnableAtomicProjectionEngineOptimizedCatchup()
        {
            AtomicProjectionEngineOptimizedCatchup = true;
        }

        public static void DisableAtomicProjectionEngineOptimizedCatchup()
        {
            AtomicProjectionEngineOptimizedCatchup = false;
        }

        /// <summary>
        /// Flush interval in milliseconds for the deferred UpdateVersion pipeline.
        /// A single shared timer fires at this interval to trigger partial batch flushes
        /// across all AtomicMongoCollectionWrapper instances.
        /// Default: 2000ms.
        /// </summary>
        public static int DeferredUpdateVersionFlushIntervalMs { get; private set; } = 5000;

        /// <summary>
        /// Maximum number of items to batch before flushing in the deferred UpdateVersion pipeline.
        /// When the batch reaches this size, it is flushed immediately without waiting for the timer.
        /// Default: 50.
        /// </summary>
        public static int DeferredUpdateVersionMaxBatchSize { get; private set; } = 50;

        public static void ConfigureDeferredUpdateVersion(int flushIntervalMs = 5000, int maxBatchSize = 50)
        {
            if (flushIntervalMs < 100)
            {
                throw new ArgumentOutOfRangeException(nameof(flushIntervalMs), flushIntervalMs, "Flush interval must be >= 100ms.");
            }
            if (maxBatchSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBatchSize), maxBatchSize, "Max batch size must be >= 1.");
            }
            DeferredUpdateVersionFlushIntervalMs = flushIntervalMs;
            DeferredUpdateVersionMaxBatchSize = maxBatchSize;
        }
    }
}

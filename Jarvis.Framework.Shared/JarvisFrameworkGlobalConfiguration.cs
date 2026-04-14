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
        /// Default: 5000ms.
        /// </summary>
        public static int DeferredUpdateVersionFlushIntervalMs { get; private set; } = 5000;

        /// <summary>
        /// Maximum number of items to batch before flushing in the deferred UpdateVersion pipeline.
        /// When the batch reaches this size, it is flushed immediately without waiting for the timer.
        /// Default: 50.
        /// </summary>
        public static int DeferredUpdateVersionMaxBatchSize { get; private set; } = 50;

        /// <summary>
        /// Configures the deferred UpdateVersion pipeline for Atomic Readmodels.
        /// </summary>
        /// <param name="flushIntervalMs">
        /// How often (in ms) the shared timer triggers partial batch flushes.
        /// Default: 5000ms. Must be &gt;= 100ms.
        /// </param>
        /// <param name="maxBatchSize">
        /// Maximum number of items collected before an automatic flush is triggered.
        /// Default: 50. Must be &gt;= 1.
        /// </param>
        /// <remarks>
        /// <para>
        /// Call this method during application startup, <strong>before</strong> any
        /// <c>AtomicMongoCollectionWrapper</c> instance is used for the first time with
        /// <c>DeferUpdateVersionAsync</c>. The timer interval is captured when the shared
        /// coordinator timer is first started; changes made after the first deferred write
        /// take effect only after the next full flush (e.g. after engine restart or
        /// <c>DeferredUpdateVersionCoordinator.StopAndFlushAllAsync</c>).
        /// </para>
        /// </remarks>
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

using System;

namespace Jarvis.Framework.Shared
{
    public static class JarvisFrameworkGlobalConfiguration
    {
        public static Boolean OfflineEventsReadmodelIdempotencyCheck { get; private set; }

        public static Boolean AtomicProjectionEngineOptimizedCatchup { get; private set; }

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
    }
}

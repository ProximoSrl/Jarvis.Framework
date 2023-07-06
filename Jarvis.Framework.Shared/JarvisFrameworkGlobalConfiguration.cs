using System;
using System.Runtime.CompilerServices;

namespace Jarvis.Framework.Shared
{
    public static class JarvisFrameworkGlobalConfiguration
    {
        public static Boolean IsMongodbLinq3Enabled { get; private set; }

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

        /// <summary>
        /// Enable mongodb driver linq3 support (introduced with 2.19 version of driver and full of bug in that version);
        /// </summary>
        public static void EnableMongodbDriverLinq3()
        {
            IsMongodbLinq3Enabled = true;
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

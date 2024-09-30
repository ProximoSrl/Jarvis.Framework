using System;
using System.Runtime.CompilerServices;

namespace Jarvis.Framework.Shared
{
    public static class JarvisFrameworkGlobalConfiguration
    {
        /// <summary>
        /// From version 7.8.2 Linq3 is enabled by default. You can disable if you want
        /// but remember that version 2 will be removed in future version of Mongodb driver.
        /// </summary>
        public static Boolean IsMongodbLinq3Enabled { get; private set; } = true;

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
        /// Enable mongodb driver linq2, if you have problem with the new default that is version 3 of Mongodb Linq provider.
        /// Version 3 historically has bug, but now seems to be stable and version 2 will be removed in future version so
        /// evaluate if you really need to use version 2 as fallback, because will be removed in future versions.
        /// </summary>
        public static void EnableMongodbDriverLinq2()
        {
            IsMongodbLinq3Enabled = false;
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

using System;

namespace Jarvis.Framework.Shared
{
    public static class JarvisFrameworkGlobalConfiguration
    {
        public static Boolean MetricsEnabled { get; private set; }

        /// <summary>
        /// If true the RepositoryCommandHandler will use SingleAggregateRepository cached
        /// to reuse the same Repository+Aggregate if found in cache. This form of aggressive
        /// cache can really improve performances when lots of commands are send to the very
        /// same aggregate.
        /// </summary>
        public static Boolean SingleAggregateRepositoryCacheEnabled { get; private set; }

        public static Boolean OfflineEventsReadmodelIdempotencyCheck { get; private set; }

        static JarvisFrameworkGlobalConfiguration()
        {
            MetricsEnabled = true;
            SingleAggregateRepositoryCacheEnabled = false;
        }

        public static void DisableMetrics()
        {
            MetricsEnabled = false;
        }

        public static void EnableMetrics()
        {
            MetricsEnabled = true;
        }

        public static void DisableSingleAggregateRepositoryCache()
        {
            SingleAggregateRepositoryCacheEnabled = false;
        }

        public static void EnableSingleAggregateRepositoryCache()
        {
            SingleAggregateRepositoryCacheEnabled = true;
        }

        public static void EnableOfflineEventsReadmodelIdempotencyCheck()
        {
            OfflineEventsReadmodelIdempotencyCheck = true;
        }

        public static void DisableOfflineEventsReadmodelIdempotencyCheck()
        {
            OfflineEventsReadmodelIdempotencyCheck = false;
        }
    }
}

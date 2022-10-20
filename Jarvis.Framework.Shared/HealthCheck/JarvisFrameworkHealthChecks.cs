using Jarvis.Framework.Shared.Support;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Shared.HealthCheck
{
    /// <summary>
    /// This is the very same class of Metrics.NET library, copied into framework to 
    /// make the transition from Metrics.NET to App.Metrics easier.
    /// </summary>
    internal struct HealthStatus
    {
        /// <summary>
        /// Flag indicating whether any checks are registered
        /// </summary>
        public readonly bool HasRegisteredChecks;

        /// <summary>
        /// All health checks passed.
        /// </summary>
        public readonly bool IsHealthy;

        /// <summary>
        /// Result of each health check operation
        /// </summary>
        public readonly JarvisFrameworkHealthCheck.Result[] Results;

        public HealthStatus(IEnumerable<JarvisFrameworkHealthCheck.Result> results)
        {
            Results = results.ToArray();
            IsHealthy = Results.All(r => r.Check.IsHealthy);
            HasRegisteredChecks = Results.Length > 0;
        }
    }

    /// <summary>
    /// Registry for health checks
    /// </summary>
    internal static class JarvisFrameworkHealthChecks
    {
        private static readonly ConcurrentDictionary<string, JarvisFrameworkHealthCheck> checks = new ConcurrentDictionary<string, JarvisFrameworkHealthCheck>();

        /// <summary>
        /// Registers an action to monitor. If the action throws the health check fails, otherwise is successful.
        /// </summary>
        /// <param name="name">Name of the health check.</param>
        /// <param name="check">Action to execute.</param>
        public static void RegisterHealthCheck(string name, Action check)
        {
            RegisterHealthCheck(new JarvisFrameworkHealthCheck(name, check));
        }

        /// <summary>
        /// Registers an action to monitor. If the action throws the health check fails, 
        /// otherwise is successful and the returned string is used as status message.
        /// </summary>
        /// <param name="name">Name of the health check.</param>
        /// <param name="check">Function to execute.</param>
        public static void RegisterHealthCheck(string name, Func<string> check)
        {
            RegisterHealthCheck(new JarvisFrameworkHealthCheck(name, check));
        }

        /// <summary>
        /// Registers a function to monitor. If the function throws or returns an HealthCheckResult.Unhealthy the check fails,
        /// otherwise the result of the function is used as a status.
        /// </summary>
        /// <param name="name">Name of the health check.</param>
        /// <param name="check">Function to execute</param>
        public static void RegisterHealthCheck(string name, Func<JarvisFrameworkHealthCheckResult> check)
        {
            RegisterHealthCheck(new JarvisFrameworkHealthCheck(name, check));
        }

        /// <summary>
        /// Registers a custom health check.
        /// </summary>
        /// <param name="healthCheck">Custom health check to register.</param>
        public static void RegisterHealthCheck(JarvisFrameworkHealthCheck healthCheck)
        {
            if (MetricsGlobalSettings.IsHealthCheckEnabled)
            {
                checks.AddOrUpdate(healthCheck.Name, healthCheck, (key, old) => healthCheck);
            }
        }

        public static void UnregisterHealthCheck(string healthCheckName)
        {
            checks.TryRemove(healthCheckName, out var _);
        }

        /// <summary>
        /// Execute all registered checks and return overall.
        /// </summary>
        /// <returns>Status of the system.</returns>
        public static HealthStatus GetStatus()
        {
            var results = checks.Values.Select(v => v.Execute()).OrderBy(r => r.Name);
            return new HealthStatus(results);
        }

        /// <summary>
        /// Remove all the registered health checks.
        /// </summary>
        public static void UnregisterAllHealthChecks()
        {
            checks.Clear();
        }
    }
}

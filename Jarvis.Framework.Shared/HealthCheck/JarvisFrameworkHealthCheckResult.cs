using System;
using System.Linq;
using System.Text;

namespace Jarvis.Framework.Shared.HealthCheck
{
    /// <summary>
    /// This is the very same class of Metrics.NET library, copied into framework to 
    /// make the transition from Metrics.NET to App.Metrics easier.
    /// </summary>
    internal struct JarvisFrameworkHealthCheckResult
    {
        /// <summary>
        /// True if the check was successful, false if the check failed.
        /// </summary>
        public readonly bool IsHealthy;

        /// <summary>
        /// Status message of the check. A status can be provided for both healthy and unhealthy states.
        /// </summary>
        public readonly string Message;

        private JarvisFrameworkHealthCheckResult(bool isHealthy, string message)
        {
            IsHealthy = isHealthy;
            Message = message;
        }

        /// <summary>
        /// Create a healthy status response.
        /// </summary>
        /// <returns>Healthy status response.</returns>
        public static JarvisFrameworkHealthCheckResult Healthy()
        {
            return Healthy("OK");
        }

        /// <summary>
        /// Create a healthy status response.
        /// </summary>
        /// <param name="message">Status message.</param>
        /// <param name="values">Values to format the status message with.</param>
        /// <returns>Healthy status response.</returns>
        public static JarvisFrameworkHealthCheckResult Healthy(string message)
        {
            return new JarvisFrameworkHealthCheckResult(true, string.IsNullOrWhiteSpace(message) ? "OK" : message);
        }

        /// <summary>
        /// Create a healthy status response with message format.
        /// </summary>
        /// <param name="message">Status message.</param>
        /// <param name="values">Values to format the status message with.</param>
        /// <returns>Healthy status response.</returns>
        public static JarvisFrameworkHealthCheckResult Healthy(string message, params object[] values)
        {
            var status = string.Format(message, values);
            return new JarvisFrameworkHealthCheckResult(true, string.IsNullOrWhiteSpace(status) ? "OK" : status);
        }

        /// <summary>
        /// Create a unhealthy status response.
        /// </summary>
        /// <returns>Unhealthy status response.</returns>
        public static JarvisFrameworkHealthCheckResult Unhealthy()
        {
            return Unhealthy("FAILED");
        }

        /// <summary>
        /// Create a unhealthy status response.
        /// </summary>
        /// <param name="message">Status message.</param>
        /// <param name="values">Values to format the status message with.</param>
        /// <returns>Unhealthy status response.</returns>
        public static JarvisFrameworkHealthCheckResult Unhealthy(string message)
        {
            return new JarvisFrameworkHealthCheckResult(false, string.IsNullOrWhiteSpace(message) ? "FAILED" : message);
        }

        /// <summary>
        /// Create a unhealthy status response if you need to use string.Format.
        /// </summary>
        /// <param name="message">Status message.</param>
        /// <param name="values">Values to format the status message with.</param>
        /// <returns>Unhealthy status response.</returns>
        public static JarvisFrameworkHealthCheckResult Unhealthy(string message, params object[] values)
        {
            var status = string.Format(message, values);
            return new JarvisFrameworkHealthCheckResult(false, string.IsNullOrWhiteSpace(status) ? "FAILED" : status);
        }

        /// <summary>
        /// Create a unhealthy status response.
        /// </summary>
        /// <param name="exception">Exception to use for reason.</param>
        /// <returns>Unhealthy status response.</returns>
        public static JarvisFrameworkHealthCheckResult Unhealthy(Exception exception)
        {
            var status = $"EXCEPTION: {exception.GetType().Name} - {exception.Message}";
            return new JarvisFrameworkHealthCheckResult(false, status + Environment.NewLine + FormatStackTrace(exception));
        }

        private static string FormatStackTrace(Exception exception, int indent = 2)
        {
            StringBuilder builder = new StringBuilder();

            var aggregate = exception as AggregateException;
            var pad = new string(' ', indent * 2);
            if (aggregate != null)
            {
                builder.AppendFormat("{0}{1}: {2}" + Environment.NewLine, pad, exception.GetType().Name, exception.Message);

                foreach (var inner in aggregate.InnerExceptions)
                {
                    builder.AppendLine(FormatStackTrace(inner, indent + 2));
                }
            }
            else
            {
                builder.AppendFormat("{0}{1}: {2}" + Environment.NewLine, pad, exception.GetType().Name, exception.Message);

                if (exception.StackTrace != null)
                {
                    var stackLines = exception.StackTrace.Split('\n')
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => string.Concat(pad, l.Trim()));

                    builder.AppendLine(string.Join(Environment.NewLine, stackLines));
                }
                else
                {
                    builder.AppendLine(string.Concat(pad, "[No Stacktrace]"));
                }

                if (exception.InnerException != null)
                {
                    builder.AppendLine(FormatStackTrace(exception.InnerException, indent + 2));
                }
            }

            return builder.ToString();
        }
    }
}

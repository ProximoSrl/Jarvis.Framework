using System;
using System.Diagnostics;

namespace Jarvis.Framework.Shared.HealthCheck
{
    /// <summary>
    /// This is the very same class of Metrics.NET library, copied into framework to 
    /// make the transition from Metrics.NET to App.Metrics easier.
    /// </summary>
    public class JarvisFrameworkHealthCheck
    {
        public struct Result
        {
            public readonly string Name;
            public readonly JarvisFrameworkHealthCheckResult Check;
            public long DurationInMilliseconds { get; set; }

            public Result(string name, JarvisFrameworkHealthCheckResult check, long durationInMilliseconds)
            {
                Name = name;
                Check = check;
                DurationInMilliseconds = durationInMilliseconds;
            }
        }

        private readonly Func<JarvisFrameworkHealthCheckResult> check;

        protected JarvisFrameworkHealthCheck(string name)
            : this(name, () => { })
        { }

        public JarvisFrameworkHealthCheck(string name, Action check)
            : this(name, () => { check(); return string.Empty; })
        { }

        public JarvisFrameworkHealthCheck(string name, Func<string> check)
            : this(name, () => JarvisFrameworkHealthCheckResult.Healthy(check()))
        { }

        public JarvisFrameworkHealthCheck(string name, Func<JarvisFrameworkHealthCheckResult> check)
        {
            Name = name;
            this.check = check;
        }

        public string Name { get; }

        protected virtual JarvisFrameworkHealthCheckResult Check()
        {
            return check();
        }

        public Result Execute()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                var checkResult = Check();
                return new Result(Name, checkResult, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception x)
            {
                return new Result(Name, JarvisFrameworkHealthCheckResult.Unhealthy(x), stopwatch.ElapsedMilliseconds);
            }
        }
    }
}

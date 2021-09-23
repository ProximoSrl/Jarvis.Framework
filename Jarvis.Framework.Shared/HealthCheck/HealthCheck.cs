﻿using System;

namespace Jarvis.Framework.Shared.HealthCheck
{
    /// <summary>
    /// This is the very same class of Metrics.NET library, copied into framework to 
    /// make the transition from Metrics.NET to App.Metrics easier.
    /// </summary>
    public class HealthCheck
    {
        public struct Result
        {
            public readonly string Name;
            public readonly HealthCheckResult Check;

            public Result(string name, HealthCheckResult check)
            {
                Name = name;
                Check = check;
            }
        }

        private readonly Func<HealthCheckResult> check;

        protected HealthCheck(string name)
            : this(name, () => { })
        { }

        public HealthCheck(string name, Action check)
            : this(name, () => { check(); return string.Empty; })
        { }

        public HealthCheck(string name, Func<string> check)
            : this(name, () => HealthCheckResult.Healthy(check()))
        { }

        public HealthCheck(string name, Func<HealthCheckResult> check)
        {
            Name = name;
            this.check = check;
        }

        public string Name { get; }

        protected virtual HealthCheckResult Check()
        {
            return check();
        }

        public Result Execute()
        {
            try
            {
                return new Result(Name, Check());
            }
            catch (Exception x)
            {
                return new Result(Name, HealthCheckResult.Unhealthy(x));
            }
        }
    }
}

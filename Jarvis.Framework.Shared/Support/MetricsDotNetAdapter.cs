using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Meter;
using Jarvis.Framework.Shared.HealthCheck;
using System;

namespace Jarvis.Framework.Shared.Support
{
    /// <summary>
    /// Wrapper to use App.Metrics counters in a way that is more similar to the
    /// old Metrics.Net class
    /// </summary>
    internal class JarvisFrameworkCounter
    {
        private readonly CounterOptions _counterOptions;

        /// <summary>
        /// Create a nuill counter that does not do anything
        /// </summary>
        internal JarvisFrameworkCounter()
        {
        }

        public JarvisFrameworkCounter(CounterOptions counterOptions)
        {
            _counterOptions = counterOptions;
        }

        public void Increment(string item, long ticks)
        {
            if (_counterOptions != null)
            {
                JarvisFrameworkMetricsHelper.Counter.Increment(_counterOptions, ticks, item);
            }
        }

        public void Increment(string item)
        {
            if (_counterOptions != null)
            {
                JarvisFrameworkMetricsHelper.Counter.Increment(_counterOptions, item);
            }
        }

        public void Increment(long ticks)
        {
            if (_counterOptions != null)
            {
                JarvisFrameworkMetricsHelper.Counter.Increment(_counterOptions, ticks);
            }
        }
    }

    internal class JarvisFrameworkMeter
    {
        private readonly MeterOptions _meterOptions;

        public JarvisFrameworkMeter()
        {
        }

        public JarvisFrameworkMeter(MeterOptions meterOptions)
        {
            _meterOptions = meterOptions;
        }

        public void Mark(int amount)
        {
            if (_meterOptions != null)
            {
                JarvisFrameworkMetricsHelper.Meter.Mark(_meterOptions, amount);
            }
        }
    }

    internal static class JarvisFrameworkMetric
    {
        public static JarvisFrameworkCounter Counter(string counterName, Unit measurementUnit)
        {
            if (MetricsGlobalSettings.IsCounterEnabled)
            {
                return new JarvisFrameworkCounter(new CounterOptions()
                {
                    Name = counterName,
                    MeasurementUnit = measurementUnit,
                });
            }

            return new JarvisFrameworkCounter();
        }

        public static JarvisFrameworkMeter Meter(string counterName, Unit measurementUnit, TimeUnit timeUnit = TimeUnit.Seconds)
        {
            if (MetricsGlobalSettings.IsCounterEnabled)
            {
                return new JarvisFrameworkMeter(new MeterOptions()
                {
                    Name = counterName,
                    MeasurementUnit = measurementUnit,
                    RateUnit = timeUnit,
                });
            }
            return new JarvisFrameworkMeter();
        }

        public static void Gauge(string name, Func<double> provider, Unit measurementUnit)
        {
            JarvisFrameworkMetricsHelper.CreateGauge(name, provider, measurementUnit);
        }

        public static void RegisterHealthCheck(string name, Func<HealthCheck.JarvisFrameworkHealthCheckResult> getHealthCheck)
        {
            JarvisFrameworkHealthChecks.RegisterHealthCheck(name, getHealthCheck);
        }
    }
}

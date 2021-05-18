using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Histogram;
using App.Metrics.Meter;
using App.Metrics.Timer;
using Jarvis.Framework.Shared.HealthCheck;
using System;

namespace Jarvis.Framework.Shared.Support
{
    /// <summary>
    /// Wrapper to use App.Metrics counters in a way that is more similar to the
    /// old Metrics.Net class
    /// </summary>
    public class Counter
    {
        private readonly CounterOptions _counterOptions;

        public Counter(CounterOptions counterOptions)
        {
            _counterOptions = counterOptions;
        }

        public void Increment(string item, long ticks)
        {
            MetricsHelper.Counter.Increment(_counterOptions, ticks, item);
        }

        public void Increment(string item)
        {
            MetricsHelper.Counter.Increment(_counterOptions, item);
        }

        public void Increment(long ticks)
        {
            MetricsHelper.Counter.Increment(_counterOptions, ticks);
        }
    }

    public class Meter
    {
        private readonly MeterOptions _meterOptions;

        public Meter(MeterOptions meterOptions)
        {
            _meterOptions = meterOptions;
        }

        public void Mark(int amount)
        {
            MetricsHelper.Meter.Mark(_meterOptions, amount);
        }
    }

    public class Timer
    {
        private readonly TimerOptions _timerOptions;

        public Timer(TimerOptions timerOptions)
        {
            _timerOptions = timerOptions;
        }

        public TimerContext NewContext()
        {
            return MetricsHelper.Timer.Time(_timerOptions);
        }

        public TimerContext NewContext(string userValue)
        {
            return MetricsHelper.Timer.Time(_timerOptions, userValue);
        }
    }

    public class Histogram
    {
        private readonly HistogramOptions _histogramOptions;

        public Histogram(HistogramOptions historgramOptions)
        {
            _histogramOptions = historgramOptions;
        }

        public void Update(long value)
        {
            MetricsHelper.Histogram.Update(_histogramOptions, value);
        }

        public void Update(long value, string userValue)
        {
            MetricsHelper.Histogram.Update(_histogramOptions, value, userValue);
        }
    }

    public static class Metric
    {
        public static Counter Counter(string counterName, Unit measurementUnit)
        {
            return new Counter(new CounterOptions()
            {
                Name = counterName,
                MeasurementUnit = measurementUnit,
            });
        }

        public static Meter Meter(string counterName, Unit measurementUnit, TimeUnit timeUnit)
        {
            return new Meter(new MeterOptions()
            {
                Name = counterName,
                MeasurementUnit = measurementUnit,
                RateUnit = timeUnit,
            });
        }

        public static Meter Meter(string counterName, Unit measurementUnit)
        {
            return new Meter(new MeterOptions()
            {
                Name = counterName,
                MeasurementUnit = measurementUnit,
            });
        }

        public static Histogram Histogram(string counterName, Unit measurementUnit)
        {
            return new Histogram(new HistogramOptions()
            {
                Name = counterName,
                MeasurementUnit = measurementUnit,
            });
        }

        public static Timer Timer(string counterName, Unit measurementUnit, TimeUnit timeUnit)
        {
            return new Timer(new TimerOptions()
            {
                Name = counterName,
                MeasurementUnit = measurementUnit,
                RateUnit = timeUnit,
            });
        }

        public static Timer Timer(string counterName, Unit measurementUnit)
        {
            return new Timer(new TimerOptions()
            {
                Name = counterName,
                MeasurementUnit = measurementUnit,
            });
        }

        public static void Gauge(string name, Func<double> provider, Unit measurementUnit)
        {
            MetricsHelper.CreateGauge(name, provider, measurementUnit);
        }

        public static void RegisterHealthCheck(string name, Func<HealthCheck.HealthCheckResult> getHealthCheck)
        {
            HealthChecks.RegisterHealthCheck(name, getHealthCheck);
        }
    }
}

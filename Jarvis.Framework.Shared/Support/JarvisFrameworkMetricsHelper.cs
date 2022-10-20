using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Gauge;
using App.Metrics.Histogram;
using App.Metrics.Meter;
using App.Metrics.Timer;
using System;

namespace Jarvis.Framework.Shared.Support
{
    /// <summary>
    /// Helper for jarvis framework to use metrics.
    /// </summary>
    public static class JarvisFrameworkMetricsHelper
    {
        /// <summary>
        /// This is the actual metrics root to use for all other
        /// metrics.
        /// </summary>
        internal static IMetricsRoot Metrics { get; private set; }

        internal static IMeasureGaugeMetrics Gauge => Metrics.Measure.Gauge;

        internal static IMeasureTimerMetrics Timer => Metrics.Measure.Timer;

        internal static IMeasureCounterMetrics Counter => Metrics.Measure.Counter;

        internal static IMeasureMeterMetrics Meter => Metrics.Measure.Meter;

        internal static IMeasureHistogramMetrics Histogram => Metrics.Measure.Histogram;

        internal static CounterOptions CreateCounter(string name, Unit measurementUnit) => new CounterOptions()
        {
            Name = name,
            MeasurementUnit = measurementUnit
        };

        internal static MeterOptions CreateMeter(string name, Unit measurementUnit) => new MeterOptions()
        {
            Name = name,
            MeasurementUnit = measurementUnit
        };

        /// <summary>
        /// This method must be called before using metrics, it accepts
        /// a full configured <see cref="IMetricsRoot"/> and is used
        /// to create an internal version of metrics to help you in
        /// creating metrics.
        /// </summary>
        /// <param name="metricsRoot"></param>
        public static void InitMetrics(IMetricsRoot metricsRoot)
        {
            Metrics = metricsRoot;
        }

        internal static void CreateGauge(string name, Func<double> provider, Unit unit)
        {
            if (MetricsGlobalSettings.IsGaugesEnabled)
            {
                var options = new GaugeOptions()
                {
                    Name = name,
                    MeasurementUnit = unit,
                };
                Metrics.Measure.Gauge.SetValue(options, provider);
            }
        }
    }
}

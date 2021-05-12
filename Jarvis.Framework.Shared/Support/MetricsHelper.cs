using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Gauge;
using App.Metrics.Histogram;
using App.Metrics.Meter;
using App.Metrics.Timer;
using Jarvis.Framework.Shared.Exceptions;
using System;

namespace Jarvis.Framework.Shared.Support
{
    public static class MetricsHelper
    {
        /// <summary>
        /// This is the actual metrics root to use for all other
        /// metrics.
        /// </summary>
        public static IMetricsRoot Metrics { get; private set; }

        public static IMeasureGaugeMetrics Gauge => Metrics.Measure.Gauge;

        public static IMeasureTimerMetrics Timer => Metrics.Measure.Timer;

        public static IMeasureCounterMetrics Counter => Metrics.Measure.Counter;

        public static IMeasureMeterMetrics Meter => Metrics.Measure.Meter;

        public static IMeasureHistogramMetrics Histogram => Metrics.Measure.Histogram;

        public static CounterOptions CreateCounter(string name, Unit measurementUnit) => new CounterOptions()
        {
            Name = name,
            MeasurementUnit = measurementUnit
        };

        public static MeterOptions CreateMeter(string name, Unit measurementUnit) => new MeterOptions()
        {
            Name = name,
            MeasurementUnit = measurementUnit
        };

        /// <summary>
        /// This method must be called before using metrics, it accepts
        /// a full configured <see cref="MetricsBuilder"/> and is used
        /// to create an internal version of metrics to help you in
        /// creating metrics.
        /// </summary>
        /// <param name="builder"></param>
        public static void InitMetrics(IMetricsBuilder builder)
        {
            if (Metrics != null)
            {
                throw new JarvisFrameworkEngineException("Metrics already configured, cannot configure more than once");
            }
            Metrics = builder.Build();
        }

        public static void CreateGauge(string name, Func<double> provider, Unit unit)
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

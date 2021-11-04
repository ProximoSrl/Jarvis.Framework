namespace Jarvis.Framework.Shared.Support
{
    public static class MetricsGlobalSettings
    {
        public static bool GlobalDisabled { get; set; }

        public static bool GaugesDisabled { get; set; }

        public static bool HealthCheckDisabled { get; set; }

        public static bool CounterDisabled { get; set; }

        public static bool TimersDisabled { get; set; }

        public static bool HistogramDisabled { get; set; }

        public static bool MeterDisabled { get; set; }

        public static bool IsHealthCheckEnabled => !GlobalDisabled && !HealthCheckDisabled;

        public static bool IsCounterEnabled => !GlobalDisabled && !CounterDisabled;

        public static bool IsGaugesEnabled => !GlobalDisabled && !GaugesDisabled;

        public static bool IsTimersEnabled => !GlobalDisabled && !TimersDisabled;

        public static bool IsHistogramEnabled => !GlobalDisabled && !HistogramDisabled;

        public static bool IsMeterEnabled => !GlobalDisabled && !MeterDisabled;
    }
}

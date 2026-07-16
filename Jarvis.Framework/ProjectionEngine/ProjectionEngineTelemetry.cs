namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    /// <summary>
    /// Stable OpenTelemetry registration names published by the projection engine.
    /// </summary>
    public static class ProjectionEngineTelemetry
    {
        /// <summary>
        /// Register this meter with OpenTelemetry to collect projection engine metrics.
        /// </summary>
        public const string MeterName = "Jarvis.Framework.ProjectionEngine";
    }
}

namespace Jarvis.Framework.Shared.Logging
{
    public static class LoggingConstants
    {
        public const string CommandId = "commandId";
        public const string CommandDescription = "commandDesc";
        public const string UserId = "commandUser";

        /// <summary>
        /// This is an important property, it is used to correlate logs in a flow.
        /// </summary>
        public const string CorrleationId = "correlationId";
    }
}

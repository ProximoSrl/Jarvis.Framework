using log4net;
using Rebus.Config;

namespace Jarvis.Framework.Bus.Rebus.Integration.Logging
{
    public static class Log4NetLoggingExtension
    {
        /// <summary>
        /// Default Log4Net thread context key to use for setting the correlation ID of the message currently being handled.
        /// </summary>
        public const string DefaultCorrelationIdPropertyKey = "CorrelationId";

        /// <summary>
        /// Configures Rebus to use Log4net for all of its internal logging. Will automatically add a 'CorrelationId' variable to the Log4Net
        /// thread context when handling messages, allowing log output to include that.
        /// </summary>
        public static void Log4Net(this RebusLoggingConfigurer configurer)
        {
            configurer.Use(new Log4NetLoggerFactory());
            //SetUpEventHandler(configurer, DefaultCorrelationIdPropertyKey);
        }

        ///// <summary>
        ///// Configures Rebus to use Log4net for all of its internal logging. Will automatically add a correlation ID variable to the Log4Net
        ///// thread context under the key specified by <paramref name="overriddenCorrelationIdPropertyKey"/> when handling messages, 
        ///// allowing log output to include that.
        ///// </summary>
        //public static void Log4Net(this RebusLoggingConfigurer configurer, string overriddenCorrelationIdPropertyKey)
        //{
        //    configurer.Use(new Log4NetLoggerFactory());

        //    SetUpEventHandler(configurer, overriddenCorrelationIdPropertyKey);
        //}

        //static void SetUpEventHandler(RebusConfigurer configurer, string correlationIdPropertyKey)
        //{
        //    configurer.Backbone.ConfigureEvents(e =>
        //    {
        //        e.BeforeTransportMessage +=
        //            (bus, message) =>
        //            {
        //                var correlationid = message.Headers.ContainsKey(Headers.CorrelationId)
        //                                        ? message.Headers[Headers.CorrelationId]
        //                                        : null;

        //                ThreadContext.Properties[correlationIdPropertyKey] = correlationid;
        //            };
        //    });
        //}
    }
}

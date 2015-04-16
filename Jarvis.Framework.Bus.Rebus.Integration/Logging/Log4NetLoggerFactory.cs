using System;
using log4net;
using Rebus.Logging;
using ILog = Rebus.Logging.ILog;

namespace Jarvis.Framework.Bus.Rebus.Integration.Logging
{
    /// <summary>
    /// Implementation of <see cref="IRebusLoggerFactory"/> that creates loggers that log
    /// their stuff using Log4net
    /// </summary>
    public class Log4NetLoggerFactory : AbstractRebusLoggerFactory
    {
        /// <summary>
        /// Gets a logger for the specified type - will delegate the call to Log4Net's log manager
        /// </summary>
        protected override ILog GetLogger(Type type)
        {
            return new Log4NetLogger(LogManager.GetLogger(type));
        }
    }
}
using System;
using Rebus.Logging;

namespace Jarvis.Framework.Bus.Rebus.Integration.Logging
{
    class Log4NetLogger : ILog
    {
        readonly log4net.ILog log;

        public Log4NetLogger(log4net.ILog log)
        {
            this.log = log;
        }

        public void Debug(string message, params object[] objs)
        {
            log.DebugFormat(message, objs);
        }

        public void Info(string message, params object[] objs)
        {
            log.InfoFormat(message, objs);
        }

        public void Warn(string message, params object[] objs)
        {
            log.WarnFormat(message, objs);
        }

        public void Error(Exception exception, string message, params object[] objs)
        {
            try
            {
                log.Error(string.Format(message, objs), exception);
            }
            catch
            {
                log.WarnFormat("Could not render string with arguments: {0}", message);
                log.Error(message, exception);
            }
        }

        public void Error(string message, params object[] objs)
        {
            log.ErrorFormat(message, objs);
        }
    }
}

using System;
using Castle.Core.Logging;
using NEventStore.Logging;

namespace Jarvis.Framework.Shared.Logging
{
    public class NEventStoreLog4NetLogger : ILog
    {
        private readonly ILogger _logger;

        public bool IsVerboseEnabled
        {
            get
            {
                return _logger.IsDebugEnabled;
            }
        }

        public bool IsDebugEnabled
        {
            get
            {
                return _logger.IsDebugEnabled;
            }
        }

        public bool IsInfoEnabled
        {
            get
            {
                return _logger.IsInfoEnabled;
            }
        }

        public LogLevel LogLevel
        {
            get
            {
                if (_logger.IsDebugEnabled)
                    return LogLevel.Debug;
                if (_logger.IsInfoEnabled)
                    return LogLevel.Info;
                if (_logger.IsWarnEnabled)
                    return LogLevel.Warn;

                return LogLevel.Error;
            }
        }

        public NEventStoreLog4NetLogger(ILogger logger)
        {
            this._logger = logger;
        }

        public virtual void Verbose(string message, params object[] values)
        {
            if (this._logger.IsDebugEnabled)
                this._logger.DebugFormat(message, values);
        }
        public virtual void Debug(string message, params object[] values)
        {
            if (this._logger.IsDebugEnabled)
                this._logger.DebugFormat(message, values);
        }
        public virtual void Info(string message, params object[] values)
        {
            if (this._logger.IsInfoEnabled)
                this._logger.InfoFormat(message, values);
        }
        public virtual void Warn(string message, params object[] values)
        {
            if (this._logger.IsWarnEnabled)
                this._logger.WarnFormat(message, values);
        }
        public virtual void Error(string message, params object[] values)
        {
            if (this._logger.IsErrorEnabled)
                this._logger.ErrorFormat(message, values);
        }
        public virtual void Fatal(string message, params object[] values)
        {
            if (this._logger.IsFatalEnabled)
                this._logger.FatalFormat(message, values);
        }
    }
}

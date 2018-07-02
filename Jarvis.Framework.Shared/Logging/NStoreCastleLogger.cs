using Castle.Core.Logging;
using System;
using NStore.Core.Logging;
using Jarvis.Framework.Shared.Support;

namespace Jarvis.Framework.Shared.Logging
{
	public class NStoreCastleLogger : INStoreLogger
    {
        private readonly ILogger _logger;

        public NStoreCastleLogger(ILogger logger)
        {
            _logger = logger;
        }

        public bool IsDebugEnabled => _logger.IsDebugEnabled;

        public bool IsWarningEnabled => _logger.IsWarnEnabled;

        public bool IsInformationEnabled => _logger.IsInfoEnabled;

		public IDisposable BeginScope<TState>(TState state)
		{
			return DisposableAction.Empty;
		}

		public void LogDebug(string message, params object[] args)
        {
            if (args.Length == 0)
                _logger.Debug(message);
            else
                _logger.DebugFormat(message, args);
        }

        public void LogError(string message, params object[] args)
        {
            if (args.Length == 0)
                _logger.Error(message);
            else
                _logger.ErrorFormat(message, args);
        }

        public void LogInformation(string message, params object[] args)
        {
            if (args.Length == 0)
                _logger.Info(message);
            else
                _logger.InfoFormat(message, args);
        }

        public void LogWarning(string message, params object[] args)
        {
            if (args.Length == 0)
                _logger.Warn(message);
            else
                _logger.WarnFormat(message, args);
        }
    }
}

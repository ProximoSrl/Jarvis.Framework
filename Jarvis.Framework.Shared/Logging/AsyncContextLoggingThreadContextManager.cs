using System;
using System.Collections.Generic;
using System.Threading;

namespace Jarvis.Framework.Shared.Logging
{
    public class AsyncContextLoggingThreadContextManager : ILoggerThreadContextManager
    {
        private readonly AsyncLocal<Dictionary<String, Object>> _asyncLocal;

        public AsyncContextLoggingThreadContextManager()
        {
            _asyncLocal = new AsyncLocal<Dictionary<String, Object>>();
        }

        public void ClearContextProperty(string propertyName)
        {
            _asyncLocal.Value?.Remove(propertyName);
        }

        public IDictionary<string, object> GetProperties()
        {
            return _asyncLocal.Value;
        }

        public void SetContextProperty(string propertyName, object propertyValue)
        {
            if (_asyncLocal.Value == null)
            {
                _asyncLocal.Value = new Dictionary<string, object>();
            }
            _asyncLocal.Value[propertyName] = propertyValue;
        }
    }
}

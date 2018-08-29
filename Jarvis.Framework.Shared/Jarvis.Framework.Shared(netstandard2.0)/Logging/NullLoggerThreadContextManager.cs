using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Jarvis.Framework.Shared.Logging
{
    public class NullLoggerThreadContextManager : ILoggerThreadContextManager
    {
        public static readonly NullLoggerThreadContextManager Instance = new NullLoggerThreadContextManager();
        private static readonly ReadOnlyDictionary<String, Object> _emptyDictionary = new ReadOnlyDictionary<String, Object>(new Dictionary<String, Object>());

        public void ClearContextProperty(string propertyName)
        {
            // Method intentionally left empty.
        }

        public IDictionary<string, object> GetProperties()
        {
            return _emptyDictionary;
        }

        public void SetContextProperty(string propertyName, object propertyValue)
        {
            // Method intentionally left empty.
        }
    }
}

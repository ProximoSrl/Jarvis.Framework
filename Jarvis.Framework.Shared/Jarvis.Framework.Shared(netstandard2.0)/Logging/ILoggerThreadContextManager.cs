using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Logging
{
    /// <summary>
    /// Abstract the concept of setting context properties for logging. Necessary because the
    /// IExtendedLogger abstraction is only valid with NLogger or Log4Net. Modified because
    /// serilog uses a nasty IDisposable based override that is really clumsy.
    /// </summary>
    public interface ILoggerThreadContextManager
    {
        /// <summary>
        /// Set a property inside the thread context of the logging infrastructure
        /// and returns a disposable that can be used to remove the value
        /// from the thread context.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <returns></returns>
        void SetContextProperty(String propertyName, Object propertyValue);

        /// <summary>
        /// Clear value of a property removing from the internal dictionary
        /// </summary>
        /// <param name="propertyName"></param>
        void ClearContextProperty(String propertyName);

        /// <summary>
        /// Return the entire list of thread property.
        /// </summary>
        /// <returns></returns>
        IDictionary<String, Object> GetProperties();
    }
}

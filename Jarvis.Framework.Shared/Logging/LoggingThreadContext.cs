using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Logging
{
	/// <summary>
	/// Helps setting some log properties that are active during current flow of
	/// execution. It is needed because serilog has some problem using context
	/// properties (it slows a lot rebuild and it seems not to be so performant
	/// </summary>
	public static class LoggingThreadContext
	{
		public const String LogContextDictionaryKey = "FF23A398-5292-4E75-B7AE-AFB9E8DCCA6A";

		/// <summary>
		/// Get a dictionary where we set log properties bound to the current flow of execution.
		/// </summary>
		/// <returns></returns>
		private static ConcurrentDictionary<String, Object> LogContextDictionary
		{
			get
			{
				var dic = CallContext.LogicalGetData(LogContextDictionaryKey) as ConcurrentDictionary<String, Object>;
				if (dic == null)
				{
					dic = new ConcurrentDictionary<string, object>();
					CallContext.LogicalSetData(LogContextDictionaryKey, dic);
				}
				return dic;
			}
		}

		public static ConcurrentDictionary<String, Object> GetLogContextDictionary()
		{
			return CallContext.LogicalGetData(LogContextDictionaryKey) as ConcurrentDictionary<String, Object>;
		}

		/// <summary>
		/// This is a simple thread properties that are completely separated by the loggin engine.
		/// </summary>
		/// <param name="propertyName"></param>
		/// <param name="propertyValue"></param>
		public static void AddThreadContextLogProperty(String propertyName, Object propertyValue)
		{
			LogContextDictionary[propertyName] = propertyValue;
		}

		public static void ClearThreadContextLogProperty(String propertyName)
		{
			Object value;
			LogContextDictionary.TryRemove(propertyName, out value);
		}
	}
}

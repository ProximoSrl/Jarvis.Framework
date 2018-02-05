using Castle.Core.Logging;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Support.Jarvis.Common.Shared.Utils;

namespace Jarvis.Framework.Shared.Logging
{
	public static class LoggingHelper
	{
		public static IDisposable MarkCommandExecution(this ILoggerThreadContextManager loggerThreadContextManager, ICommand command)
		{
			DisposableStack disposableStack = new DisposableStack();
			disposableStack.Push(loggerThreadContextManager.SetContextProperty(LoggingConstants.CommandId, command?.MessageId));
			disposableStack.Push(loggerThreadContextManager.SetContextProperty(LoggingConstants.UserId, command?.GetContextData(MessagesConstants.UserId)));
			disposableStack.Push(loggerThreadContextManager.SetContextProperty(LoggingConstants.CommandDescription, String.Format("{0} [{1}]", command?.Describe(), command?.GetType())));
			return disposableStack;
		}
	}

	/// <summary>
	/// Abstract the concept of setting context properties for logging. Necessary because the
	/// IExtendedLogger abstraction is only valid with NLogger or Log4Net
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
		IDisposable SetContextProperty(String propertyName, Object propertyValue);
	}

	public class NullLoggerThreadContextManager : ILoggerThreadContextManager
	{
		public static readonly NullLoggerThreadContextManager Instance = new NullLoggerThreadContextManager();
		public IDisposable SetContextProperty(string propertyName, object propertyValue)
		{
			return DisposableAction.Empty;
		}
	}
}

using Castle.Core.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Jarvis.Framework.TestHelpers
{
	public class TestLogger : ILogger
	{
		/// <summary>
		/// Set this static variable to true to enable output to console,
		/// this is needed to avoid hammering console output.
		/// </summary>
		public static Boolean GlobalEnabled { get; set; } = false;

		public Int32 ErrorCount { get; set; }

		// Fields
		private string name;
		// Methods
		public TestLogger()
		{
			this.name = "unnamed";
			Logs = new List<TestLog>();
		}

		public TestLogger(LoggerLevel loggerLevel)
		{
			this.name = "unnamed";
			Level = loggerLevel;
			Logs = new List<TestLog>();
		}

		public TestLogger(string name)
		{
			this.name = "unnamed";
			ChangeName(name);
			Logs = new List<TestLog>();
		}

		public TestLogger(string loggerName, LoggerLevel loggerLevel)
			: this(loggerLevel)
		{
			Logs = new List<TestLog>();
			ChangeName(loggerName);
		}

		protected void ChangeName(string newName)
		{
			this.name = newName ?? throw new ArgumentNullException(nameof(newName));
		}

		public ILogger CreateChildLogger(string loggerName)
		{
			if (loggerName == null)
			{
				throw new ArgumentNullException(nameof(loggerName), "To create a child logger you must supply a non null name");
			}
			return new ConsoleLogger(string.Format(CultureInfo.CurrentCulture, "{0}.{1}", new object[] { Name, loggerName }));
		}

		public void Debug(string message)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Debug, message, null);
			}
		}

		public void Debug(string message, Exception exception)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Debug, message, exception);
			}
		}

		public void Debug(string format, params object[] args)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Debug, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void DebugFormat(string format, params object[] args)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Debug, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void DebugFormat(Exception exception, string format, params object[] args)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Debug, string.Format(CultureInfo.CurrentCulture, format, args), exception);
			}
		}

		public void DebugFormat(IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Debug, string.Format(formatProvider, format, args), null);
			}
		}

		public void DebugFormat(Exception exception, IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Debug, string.Format(formatProvider, format, args), exception);
			}
		}

		public void Trace(string message)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Trace, message, null);
			}
		}

		public void Trace(string message, Exception exception)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Trace, message, exception);
			}
		}

		public void Trace(string format, params object[] args)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Trace, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void TraceFormat(string format, params object[] args)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Trace, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void TraceFormat(Exception exception, string format, params object[] args)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Trace, string.Format(CultureInfo.CurrentCulture, format, args), exception);
			}
		}

		public void TraceFormat(IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Trace, string.Format(formatProvider, format, args), null);
			}
		}

		public void TraceFormat(Exception exception, IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsDebugEnabled)
			{
				this.Log(LoggerLevel.Trace, string.Format(formatProvider, format, args), exception);
			}
		}

		public void Error(string message)
		{
			if (this.IsErrorEnabled)
			{
				this.Log(LoggerLevel.Error, message, null);
			}
		}

		public void Error(string message, Exception exception)
		{
			if (this.IsErrorEnabled)
			{
				this.Log(LoggerLevel.Error, message, exception);
			}
		}

		public void Error(string format, params object[] args)
		{
			if (this.IsErrorEnabled)
			{
				this.Log(LoggerLevel.Error, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void ErrorFormat(string format, params object[] args)
		{
			if (this.IsErrorEnabled)
			{
				this.Log(LoggerLevel.Error, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void ErrorFormat(Exception exception, string format, params object[] args)
		{
			if (this.IsErrorEnabled)
			{
				this.Log(LoggerLevel.Error, string.Format(CultureInfo.CurrentCulture, format, args), exception);
			}
		}

		public void ErrorFormat(IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsErrorEnabled)
			{
				this.Log(LoggerLevel.Error, string.Format(formatProvider, format, args), null);
			}
		}

		public void ErrorFormat(Exception exception, IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsErrorEnabled)
			{
				this.Log(LoggerLevel.Error, string.Format(formatProvider, format, args), exception);
			}
		}

		public void Fatal(string message)
		{
			if (this.IsFatalEnabled)
			{
				this.Log(LoggerLevel.Fatal, message, null);
			}
		}

		public void Fatal(string format, params object[] args)
		{
			if (this.IsFatalEnabled)
			{
				this.Log(LoggerLevel.Fatal, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void Fatal(string message, Exception exception)
		{
			if (this.IsFatalEnabled)
			{
				this.Log(LoggerLevel.Fatal, message, exception);
			}
		}

		[Obsolete("Use Fatal instead")]
		public void FatalError(string message)
		{
			if (this.IsFatalEnabled)
			{
				this.Log(LoggerLevel.Fatal, message, null);
			}
		}

		[Obsolete("Use Fatal instead")]
		public void FatalError(string message, Exception exception)
		{
			if (this.IsFatalEnabled)
			{
				this.Log(LoggerLevel.Fatal, message, exception);
			}
		}

		[Obsolete("Use Fatal or FatalFormat instead")]
		public void FatalError(string format, params object[] args)
		{
			if (this.IsFatalEnabled)
			{
				this.Log(LoggerLevel.Fatal, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void FatalFormat(string format, params object[] args)
		{
			if (this.IsFatalEnabled)
			{
				this.Log(LoggerLevel.Fatal, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void FatalFormat(Exception exception, string format, params object[] args)
		{
			if (this.IsFatalEnabled)
			{
				this.Log(LoggerLevel.Fatal, string.Format(CultureInfo.CurrentCulture, format, args), exception);
			}
		}

		public void FatalFormat(IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsFatalEnabled)
			{
				this.Log(LoggerLevel.Fatal, string.Format(formatProvider, format, args), null);
			}
		}

		public void FatalFormat(Exception exception, IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsFatalEnabled)
			{
				this.Log(LoggerLevel.Fatal, string.Format(formatProvider, format, args), exception);
			}
		}

		public void Info(string message)
		{
			if (this.IsInfoEnabled)
			{
				this.Log(LoggerLevel.Info, message, null);
			}
		}

		public void Info(string format, params object[] args)
		{
			if (this.IsInfoEnabled)
			{
				this.Log(LoggerLevel.Info, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void Info(string message, Exception exception)
		{
			if (this.IsInfoEnabled)
			{
				this.Log(LoggerLevel.Info, message, exception);
			}
		}

		public void InfoFormat(string format, params object[] args)
		{
			if (this.IsInfoEnabled)
			{
				this.Log(LoggerLevel.Info, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void InfoFormat(Exception exception, string format, params object[] args)
		{
			if (this.IsInfoEnabled)
			{
				this.Log(LoggerLevel.Info, string.Format(CultureInfo.CurrentCulture, format, args), exception);
			}
		}

		public void InfoFormat(IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsInfoEnabled)
			{
				this.Log(LoggerLevel.Info, string.Format(formatProvider, format, args), null);
			}
		}

		public void InfoFormat(Exception exception, IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsInfoEnabled)
			{
				this.Log(LoggerLevel.Info, string.Format(formatProvider, format, args), exception);
			}
		}

		private void Log(LoggerLevel loggerLevel, string message, Exception exception)
		{
			this.Log(loggerLevel, this.Name, message, exception);
		}

		public class TestLog
		{
			public LoggerLevel Level { get; private set; }

			public String Text { get; private set; }

			public TestLog(LoggerLevel level, String text)
			{
				Level = level;
				Text = text;
			}
		}

		public List<TestLog> Logs { get; private set; }

		protected void Log(LoggerLevel loggerLevel, string loggerName, string message, Exception exception)
		{
			switch (loggerLevel)
			{
				case LoggerLevel.Debug:
					break;
				case LoggerLevel.Error:
					ErrorCount++;
					break;
				case LoggerLevel.Fatal:
					break;
				case LoggerLevel.Info:
					break;
				case LoggerLevel.Off:
					break;
				case LoggerLevel.Warn:
					break;
				default:
					break;
			}
			TextWriter output = Console.Out;
			if (loggerLevel == LoggerLevel.Error)
				output = Console.Error;

			output.WriteLine("[{0}] '{1}' {2}", loggerLevel, loggerName, message);
			if (exception != null)
			{
				output.WriteLine("[{0}] '{1}' {2}: {3} {4}", new object[] { loggerLevel.ToString(), loggerName, exception.GetType().FullName, exception.Message, exception.StackTrace });
			}
			Logs.Add(new TestLog(loggerLevel, message));
		}

		public void Warn(string message)
		{
			if (this.IsWarnEnabled)
			{
				this.Log(LoggerLevel.Warn, message, null);
			}
		}

		public void Warn(string format, params object[] args)
		{
			if (this.IsWarnEnabled)
			{
				this.Log(LoggerLevel.Warn, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void Warn(string message, Exception exception)
		{
			if (this.IsWarnEnabled)
			{
				this.Log(LoggerLevel.Warn, message, exception);
			}
		}

		public void WarnFormat(string format, params object[] args)
		{
			if (this.IsWarnEnabled)
			{
				this.Log(LoggerLevel.Warn, string.Format(CultureInfo.CurrentCulture, format, args), null);
			}
		}

		public void WarnFormat(Exception exception, string format, params object[] args)
		{
			if (this.IsWarnEnabled)
			{
				this.Log(LoggerLevel.Warn, string.Format(CultureInfo.CurrentCulture, format, args), exception);
			}
		}

		public void WarnFormat(IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsWarnEnabled)
			{
				this.Log(LoggerLevel.Warn, string.Format(formatProvider, format, args), null);
			}
		}

		public void WarnFormat(Exception exception, IFormatProvider formatProvider, string format, params object[] args)
		{
			if (this.IsWarnEnabled)
			{
				this.Log(LoggerLevel.Warn, string.Format(formatProvider, format, args), exception);
			}
		}

		// Properties
		public bool IsDebugEnabled
		{
			get
			{
				return Level >= LoggerLevel.Debug && GlobalEnabled;
			}
		}

		// Properties
		public bool IsTraceEnabled
		{
			get
			{
				return Level >= LoggerLevel.Trace && GlobalEnabled;
			}
		}

		public bool IsErrorEnabled
		{
			get
			{
				return Level >= LoggerLevel.Error && GlobalEnabled;
			}
		}

		public bool IsFatalEnabled
		{
			get
			{
				return Level >= LoggerLevel.Fatal && GlobalEnabled;
			}
		}

		[Obsolete("Use IsFatalEnabled instead")]
		public bool IsFatalErrorEnabled
		{
			get
			{
				return Level >= LoggerLevel.Fatal && GlobalEnabled;
			}
		}

		public bool IsInfoEnabled
		{
			get
			{
				return Level >= LoggerLevel.Info && GlobalEnabled;
			}
		}

		public bool IsWarnEnabled
		{
			get
			{
				return Level >= LoggerLevel.Warn && GlobalEnabled;
			}
		}

		public LoggerLevel Level { get; set; } = LoggerLevel.Warn;

		public string Name
		{
			get
			{
				return this.name;
			}
		}

		public void Debug(Func<string> messageFactory)
		{
			this.Log(LoggerLevel.Debug, messageFactory(), null);
		}

		public void Trace(Func<string> messageFactory)
		{
			this.Log(LoggerLevel.Trace, messageFactory(), null);
		}

		public void Error(Func<string> messageFactory)
		{
			this.Log(LoggerLevel.Error, messageFactory(), null);
		}

		public void Fatal(Func<string> messageFactory)
		{
			this.Log(LoggerLevel.Fatal, messageFactory(), null);
		}

		public void Info(Func<string> messageFactory)
		{
			this.Log(LoggerLevel.Info, messageFactory(), null);
		}

		public void Warn(Func<string> messageFactory)
		{
			this.Log(LoggerLevel.Warn, messageFactory(), null);
		}

		private readonly TestThreadProperties _threadProperties = new TestThreadProperties();
		private readonly TestGlobalProperties _globalProperties = new TestGlobalProperties();
		private readonly TestContextStacks _contextStacks = new TestContextStacks();

		public IContextProperties GlobalProperties
		{
			get { return _globalProperties; }
		}

		public IContextProperties ThreadProperties
		{
			get { return _threadProperties; }
		}

		public IContextStacks ThreadStacks
		{
			get { return _contextStacks; }
		}

		public class TestThreadProperties : IContextProperties
		{
			[ThreadStatic]
			private static Dictionary<String, Object> _container;

			private static Dictionary<String, Object> Container
			{
				get { return _container ?? (_container = new Dictionary<String, Object>()); }
			}

			public object this[string key]
			{
				get
				{
					return Container[key];
				}
				set
				{
					Container[key] = value;
				}
			}
		}

		public class TestGlobalProperties : IContextProperties
		{
			private static Dictionary<String, Object> _container = new Dictionary<String, Object>();

			public object this[string key]
			{
				get
				{
					return _container[key];
				}
				set
				{
					_container[key] = value;
				}
			}
		}

		public class TestContextStack : IContextStack
		{
			private readonly Stack<String> _stack = new Stack<string>();

			public void Clear()
			{
				_stack.Clear();
			}

			public int Count
			{
				get { return _stack.Count; }
			}

			public string Pop()
			{
				return _stack.Pop();
			}

			public IDisposable Push(string message)
			{
				_stack.Push(message);
				return new DisposableAction(() => Pop());
			}
		}

		public class TestContextStacks : IContextStacks
		{
			private static Dictionary<String, IContextStack> _container = new Dictionary<String, IContextStack>();
			public IContextStack this[string key]
			{
				get { return _container[key]; }
			}
		}

		public static IDisposable GlobalEnableStartScope()
		{
			var currentGlobalEnabled = GlobalEnabled;
			GlobalEnabled = true;
			return new DisposableAction(() => GlobalEnabled = currentGlobalEnabled);
		}

		private class DisposableAction : IDisposable
		{
			private readonly Action _action;

			public DisposableAction(Action action)
			{
				_action = action;
			}

			protected virtual void Dispose(bool disposing)
			{
				if (disposing)
				{
					_action();
				}
			}

			public void Dispose()
			{
				Dispose(true);
			}
		}
	}
}

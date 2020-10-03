using System;
using System.Collections.Generic;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Exceptions;
using NStore.Domain;
using NStore.Core.Processing;
using Fasterflect;
using System.Collections;

namespace Jarvis.Framework.Kernel.Engine
{
	public interface IProcessManagerListener
	{
		IEnumerable<Type> ListeningTo { get; }

		Type GetProcessType();
	}

	public interface IProcessManagerListener<TProcessManager, TState> : IProcessManagerListener
		where TState : class, new()
		where TProcessManager : ProcessManager<TState>
	{
		string GetCorrelationId<TMessage>(TMessage message) where TMessage : IMessage;
	}

	public abstract class AbstractProcessManagerListener<TProcessManager, TState> : IProcessManagerListener<TProcessManager, TState>
		where TState : class, new()
		where TProcessManager : ProcessManager<TState>
	{
		private readonly Dictionary<Type, Func<IMessage, string>> _correlator =
			new Dictionary<Type, Func<IMessage, string>>();

		/// <summary>
		/// Estabilish a correlation between a Message and the Id of the saga that should
		/// handle that message.
		/// </summary>
		/// <typeparam name="TMessage"></typeparam>
		/// <param name="correlate"></param>
		protected void Map<TMessage>(Func<TMessage, string> correlate)
		{
			_correlator[typeof(TMessage)] = m => Prefix + correlate((TMessage)m);
		}

		protected void MapWithoutPrefix<TMessage>(Func<TMessage, string> correlate)
		{
			_correlator[typeof(TMessage)] = m =>
			{
				var correlateId = correlate((TMessage)m);
				if (correlateId == null || !correlateId.StartsWith(Prefix))
					return null;
				return correlateId;
			};
		}

		public abstract String Prefix { get; }

		public string GetCorrelationId<TMessage>(TMessage message) where TMessage : IMessage
		{
			return _correlator[message.GetType()](message);
		}

		public IEnumerable<Type> ListeningTo
		{
			get { return _correlator.Keys; }
		}

		public Type GetProcessType()
		{
			return typeof(TProcessManager);
		}
	}

	public abstract class AbstractProcessManager<TState> : ProcessManager<TState>
		where TState : AbstractProcessManagerState, new()
	{
		public ILogger Logger { get; set; }

		protected AbstractProcessManager() : base(ProcessManagerPayloadProcessor.Instance)
		{
			Logger = NullLogger.Instance;
		}

		protected override void AfterInit()
		{
			base.AfterInit();
			State.SetLogger(Logger);
			State.SetProcessManagerId(Id);
		}

		protected override void PostLoadingProcessing()
		{
			base.PostLoadingProcessing();
			State.ReplyFinished();
		}

		protected void Throw(String message, params String[] param)
		{
			throw new JarvisFrameworkEngineException(String.Format(message, param));
		}

		protected void Throw(Exception innerException, String message, params String[] param)
		{
			throw new JarvisFrameworkEngineException(String.Format(message, param), innerException);
		}
	}

	public abstract class AbstractProcessManagerState
	{
		public ILogger Logger { get; protected set; }
		public Boolean IsInReplay { get; protected set; }

		public String ProcessManagerId { get; protected set; }

		protected AbstractProcessManagerState()
		{
			Logger = NullLogger.Instance;
			IsInReplay = true;
		}

		public void SetLogger(ILogger logger)
		{
			Logger = logger;
		}

		public void SetProcessManagerId(String processManagerId)
		{
			ProcessManagerId = processManagerId;
		}

		public void ReplyFinished()
		{
			IsInReplay = false;
		}

		protected T PrepareCommand<T>(T command) where T : ICommand
		{
			command.SetContextData(MessagesConstants.ReplyToHeader, ProcessManagerId);
			command.SetContextData(MessagesConstants.SagaIdHeader, ProcessManagerId);
			return command;
		}
	}

	public sealed class ProcessManagerPayloadProcessor : IPayloadProcessor
	{
		public static readonly IPayloadProcessor Instance = new ProcessManagerPayloadProcessor();

		private readonly string[] _methods = { "On", "StartedBy", "ContinuedBy", "CompletedBy" };

		private ProcessManagerPayloadProcessor()
		{
		}

		public object Process(object state, object payload)
		{
			foreach (var methodName in _methods)
			{
				var method = state.GetType().Method(methodName, new[] { payload.GetType() }, Flags.InstanceAnyVisibility);
				if (method != null)
				{
					var retValue = method.Call(state, new object[] { payload });
					if (retValue is IEnumerable)
					{
						//Process managers can use yield, we need to force iteration.
						List<Object> iterated = new List<object>();
						foreach (var obj in (IEnumerable)retValue)
						{
							iterated.Add(obj);
						}
						return iterated;
					}
					return retValue;
				}
			}

			return null;
		}
	}
}

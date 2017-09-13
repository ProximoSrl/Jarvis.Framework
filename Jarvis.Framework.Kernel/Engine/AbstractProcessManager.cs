using System;
using System.Collections.Generic;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Messages;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.Framework.Shared.Commands;

namespace Jarvis.Framework.Kernel.Engine
{
    public interface IObserveMessage<TMessage> where TMessage : IMessage
    {
        void On(TMessage message);
    }

    public interface IProcessManagerListener
    {
        IEnumerable<Type> ListeningTo { get; }
        Type GetProcessType();
    }

    public interface IProcessManagerListener<TProcessManager> : IProcessManagerListener where TProcessManager : ISagaEx
    {
        string GetCorrelationId<TMessage>(TMessage message) where TMessage : IMessage;
    }

    public abstract class AbstractProcessManagerListener<TProcessManager> : IProcessManagerListener<TProcessManager> where TProcessManager : ISagaEx
    {
        private Dictionary<Type, Func<IMessage, string>> _correlator = new Dictionary<Type, Func<IMessage, string>>();
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

    public abstract class AbstractProcessManager : ISagaEx, IEquatable<ISagaEx>
    {
       
        readonly ICollection<object> _uncommitted = new LinkedList<object>();
        readonly ICollection<object> _undispatched = new LinkedList<object>();

        public string Id { get; private set; }

        public int Version { get; private set; }

        public ILogger Logger { get; set; }

        public AbstractProcessManager()
        {
            Logger = NullLogger.Instance;
        }

        public void Transition(object message)
        {
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Dispatching message {0} to saga {1} [{2}] IsReplay {3}", message.GetType().Name, this.GetType().Name, this.Id, IsInReplay);
            ((dynamic)this).On((dynamic)message);
            _uncommitted.Add(message);
            Version++;
        }

        ICollection<object> ISagaEx.GetUncommittedEvents()
        {
            return _uncommitted;
        }

        void ISagaEx.ClearUncommittedEvents()
        {
            _uncommitted.Clear();
        }

        ICollection<object> ISagaEx.GetUndispatchedMessages()
        {
            return _undispatched;
        }

        void ISagaEx.ClearUndispatchedMessages()
        {
            _undispatched.Clear();
        }

        public void SetReplayMode(bool replayOn)
        {
            this.IsInReplay = replayOn;
        }

        protected bool IsInReplay { get; private set; }

        public virtual bool Equals(ISagaEx other)
        {
            return null != other && other.Id == Id;
        }

        protected void DispatchWithReply(ICommand command, String issuedBy)
        {
            command.SetContextData(MessagesConstants.SagaIdHeader, this.Id);
            command.SetContextData(MessagesConstants.ReplyToHeader, this.Id);
            Dispatch(command, issuedBy);
        }

        protected void Dispatch(IMessage message)
        {
            InnerDispatch(message);
        }

        private void InnerDispatch(IMessage command)
        {
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Saga {0} dispatched command {1}", Id, command.Describe());
            _undispatched.Add(command);
        }


        protected void Dispatch(ICommand message, String issuedBy)
        {
            message.SetContextData(MessagesConstants.UserId, issuedBy);
            Dispatch(message);
        }

        /// <summary>
        /// Dispatch a standard <see cref="SagaTimeout" /> message to current
        /// saga.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <param name="timeOutKey">key related to this timeOut, it can be null</param>
        public void DispatchTimeout(DateTime dateTime, string timeOutKey = null)
        {
            var timeout = new SagaTimeout(this.Id, timeOutKey);
            var message = new SagaDeferredMessage(timeout, dateTime);
            Dispatch(message);
        }

        public void DispatchDeferred(DateTime dateTime, IMessage message)
        {
            var deferredMessage = new SagaDeferredMessage(message, dateTime);
            Dispatch(deferredMessage);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ISagaEx);
        }

        protected void Throw(String message, params String[] param)
        {
            throw new ApplicationException(String.Format(message, param));
        }

        protected void Throw(Exception innerException, String message, params String[] param)
        {
            throw new ApplicationException(String.Format(message, param), innerException);
        }
    }
}

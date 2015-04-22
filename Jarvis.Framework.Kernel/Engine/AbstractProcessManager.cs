using System;
using System.Collections.Generic;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Messages;
using Jarvis.NEventStoreEx.CommonDomainEx;

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
        Dictionary<Type, Func<IMessage, string>> _correlator = new Dictionary<Type, Func<IMessage, string>>();

        /// <summary>
        /// Estabilish a correlation between a Message and the Id of the saga that should
        /// handle that message.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="correlate"></param>
        protected void Map<TMessage>(Func<TMessage, string> correlate)
        {
            _correlator[typeof (TMessage)] = m => correlate((TMessage) m);
        }

        public string GetCorrelationId<TMessage>(TMessage message) where TMessage : IMessage
        {
            return _correlator[message.GetType()](message);
        }

        public IEnumerable<Type> ListeningTo {
            get { return _correlator.Keys; }
        }

        public Type GetProcessType()
        {
            return typeof (TProcessManager);
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
            ((dynamic) this).On((dynamic)message);
            _uncommitted.Add(message);
            Version++;
        }

        ICollection<object> ISagaEx.GetUncommittedEvents()
        {
            return _uncommitted ;
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

        protected void Dispatch(IMessage message)
        {
            _undispatched.Add(message);
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

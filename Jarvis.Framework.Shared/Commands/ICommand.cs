using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Commands
{
    /// <summary>
    /// This is a generic interface for ICommand
    /// </summary>
    public interface ICommand : IMessage
    {
        /// <summary>
        /// This method will set a string value in the command header. 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void SetContextData(string key, string value);

        string GetContextData(string key, string defaultValue);

        string GetContextData(string key);

        void InheritContextFrom(ICommand command);

        [JsonIgnore]
        IEnumerable<string> AllContextKeys { get; }
    }

    /// <summary>
    /// We need to have a simple and non generic way to understand
    /// if a command is a command for an aggregate and what is the 
    /// id of the aggregate.
    /// </summary>
    public interface IAggregateCommand : ICommand
    {
        IIdentity AggregateId { get; }
    }

    /// <summary>
    /// Command that operate on an aggregate, and it has the aggregate id
    /// as property.
    /// </summary>
    /// <typeparam name="TIdentity"></typeparam>
    public interface ICommand<out TIdentity> : IAggregateCommand where TIdentity : IIdentity
    {
    }

    [Serializable]
    public abstract class Command : ICommand
    {
        protected Command()
        {
            Context = new Dictionary<string, string>();
            MessageId = Guid.NewGuid();
            SetContextData(MessagesConstants.CommandTimestamp, DateTime.UtcNow.ToString("o"));
        }

        [BsonDictionaryOptions(Representation = DictionaryRepresentation.ArrayOfArrays)]
        public IDictionary<string, string> Context { get; set; }

        public Guid MessageId { get; set; }

        public void SetContextData(string key, string value)
        {
            Context[key] = value;
        }

        public void DisableSuccessReply()
        {
            SetContextData("disable-success-reply", "true");
        }

        public string GetContextData(string key)
        {
            return GetContextData(key, null);
        }

        public string GetContextData(string key, string defaultValue)
        {
            return Context.ContainsKey(key) ? Context[key] : defaultValue;
        }

        public void InheritContextFrom(ICommand command)
        {
            var sourceCommand = ((Command)command);
            foreach (string key in sourceCommand.Context.Keys)
            {
                if (!Context.ContainsKey(key))
                    SetContextData(key, sourceCommand.Context[key]);
            }
        }

        public IEnumerable<string> AllContextKeys
        {
            get { return Context.Keys; }
        }

        public virtual string Describe()
        {
            return GetType().Name;
        }

        public override string ToString()
        {
            return GetType().FullName + " id: " + MessageId + "\n";
        }
    }

    [Serializable]
    public abstract class Command<TIdentity> : Command,
        IAggregateCommand,
        ICommand<TIdentity>
        where TIdentity : IIdentity
    {
        protected Command()
        {
        }

        protected Command(TIdentity aggregateId)
        {
            AggregateId = aggregateId;
        }

        public TIdentity AggregateId { get; set; }

        IIdentity IAggregateCommand.AggregateId => AggregateId;
    }
}
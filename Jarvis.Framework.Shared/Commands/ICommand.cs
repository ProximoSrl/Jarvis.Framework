using System;
using System.Collections.Generic;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Newtonsoft.Json;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace Jarvis.Framework.Shared.Commands
{
    public interface ICommand : IMessage
    {
        void SetContextData(string key, string value);
        string GetContextData(string key, string defaultValue = null);
        void InheritContextFrom(ICommand command);

        [JsonIgnore]
        IEnumerable<string> AllContextKeys { get; }
    }

    public interface ICommand<out TIdentity> : ICommand where TIdentity : IIdentity
    {
        TIdentity AggregateId { get; }
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

        public string GetContextData(string key, string defaultValue = null)
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
    public abstract class Command<TIdentity> : Command, ICommand<TIdentity> where TIdentity : IIdentity
    {
        protected Command()
        {
        }

        protected Command(TIdentity aggregateId)
        {
            AggregateId = aggregateId;
        }

        public TIdentity AggregateId { get; set; }
    }
}
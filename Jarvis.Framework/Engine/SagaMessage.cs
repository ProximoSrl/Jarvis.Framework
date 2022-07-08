using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// Base class to handle saga message.
    /// </summary>
	public class SagaMessage : IMessage
    {
        /// <summary>
        /// identificativo dell'evento
        /// </summary>
        public Guid MessageId { get; private set; }

        public String SagaId { get; private set; }

        [BsonDictionaryOptions(Representation = DictionaryRepresentation.ArrayOfArrays)]
        public IDictionary<string, string> Context { get; set; }

        public virtual string Describe()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public void SetContextData(string key, string value)
        {
            Context[key] = value;
        }

        public string GetContextData(string key)
        {
            return GetContextData(key, null);
        }

        public string GetContextData(string key, string defaultValue)
        {
            return Context.ContainsKey(key) ? Context[key] : defaultValue;
        }

        public SagaMessage(String sagaId)
        {
            MessageId = Guid.NewGuid();
            SagaId = sagaId;
            Context = new Dictionary<String, String>();
        }
    }

    public class SagaTimeout : SagaMessage
    {
        public string TimeOutKey { get; private set; }

        public SagaTimeout(String sagaId, string timeOutKey = null)
            : base(sagaId)
        {
            TimeOutKey = timeOutKey;
        }
    }

    public static class SagaExtensions
    {
        public static SagaMessage WithDiagnosticDescription(this SagaMessage command, string description)
        {
            if (description != null)
            {
                command.SetContextData("triggered-by-description", description);
            }
            return command;
        }

        public static SagaMessage WithDiagnosticTriggeredByInfo(this SagaMessage command, IMessage message, string description = null)
        {
            command.SetContextData("triggered-by", message.GetType().FullName);
            command.SetContextData("triggered-by-id", message.MessageId.ToString());

            if (message is DomainEvent @event)
            {
                command.SetContextData("triggered-by-aggregate", @event.AggregateId.AsString());
            }

            return command.WithDiagnosticDescription(description);
        }
    }
}

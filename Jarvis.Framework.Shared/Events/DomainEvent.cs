using System;
using System.Collections.Generic;
using Jarvis.Framework.Shared.IdentitySupport;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Jarvis.Framework.Shared.Events
{
    [Serializable]
    public abstract class DomainEvent : IDomainEvent
    {
        /// <summary>
        /// identificativo dell'evento
        /// </summary>
        public Guid MessageId { get; set; }

        public virtual string Describe()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Identificativo dell'utente che ha scatenato l'evento
        /// </summary>
        [BsonIgnore]
        public string IssuedBy {
            get
            {
                if (this.Context != null && this.Context.ContainsKey("user.id"))
                    return (string)this.Context["user.id"];

                return null;
            } 
        }

        /// <summary>
        /// Indica il timestamp della commit associata all'evento
        /// </summary>
        [BsonIgnore]
        public DateTime CommitStamp { get; private set; }

        /// <summary>
        /// Identificativo della commit
        /// </summary>
        [BsonIgnore]
        public Guid CommitId { get; private set; }

        /// <summary>
        /// Identificativo dell'aggregato a cui appartiene l'evento
        /// </summary>
        [BsonIgnore]
        public EventStoreIdentity AggregateId { get; private set; }

        /// <summary>
        /// Contesto dell'evento
        /// </summary>
        [BsonIgnore]
        public IDictionary<string, object> Context { get; private set; }

        /// <summary>
        /// Versione dell'aggregato al momento del raise dell'evento
        /// </summary>
        [BsonIgnore]
        public int Version { get; private set; }

        /// <summary>
        /// Commit checkpoint
        /// </summary>
        [BsonIgnore]
        public string CheckpointToken { get; private set; }

        /// <summary>
        /// Costruttore 
        /// </summary>
        protected DomainEvent()
        {
            this.MessageId = Guid.NewGuid();
        }
    }
}
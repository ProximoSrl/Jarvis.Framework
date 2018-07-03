using System;
using System.Collections.Generic;
using Jarvis.Framework.Shared.IdentitySupport;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Jarvis.Framework.Shared.Messages;

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
        public string IssuedBy
        {
            get
            {
                if (this.Context != null && this.Context.ContainsKey(MessagesConstants.UserId))
                    return (string)this.Context[MessagesConstants.UserId];

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
        public String CommitId { get; private set; }

        /// <summary>
        /// Identificativo dell'aggregato a cui appartiene l'evento
        /// </summary>
        public EventStoreIdentity AggregateId { get; private set; }

        /// <summary>
        /// This is true if this is the last event of a commit
        /// </summary>
        [BsonIgnore]
        public Boolean IsLastEventOfCommit { get; set; }

        /// <summary>
        /// Contesto dell'evento
        /// </summary>
        [BsonIgnore]
        public IDictionary<string, object> Context { get; private set; }

        /// <summary>
        /// Versione dell'aggregato al momento del raise dell'evento
        /// </summary>
        [BsonIgnore]
        public Int64 Version { get; private set; }

        /// <summary>
        /// Commit checkpoint
        /// </summary>
        [BsonIgnore]
        public Int64 CheckpointToken { get; private set; }

        /// <summary>
        /// Costruttore 
        /// </summary>
        protected DomainEvent()
        {
            this.MessageId = Guid.NewGuid();
        }
    }
}
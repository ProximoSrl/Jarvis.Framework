using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

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
        public DateTime CommitStamp { get; internal set; }

        /// <summary>
        /// Identificativo della commit
        /// </summary>
        [BsonIgnore]
        public String CommitId { get; internal set; }

        /// <summary>
        /// Identificativo dell'aggregato a cui appartiene l'evento
        /// </summary>
        public EventStoreIdentity AggregateId { get; internal set; }

        /// <summary>
        /// This is true if this is the last event of a commit
        /// </summary>
        [BsonIgnore]
        public Boolean IsLastEventOfCommit { get; set; }

        /// <summary>
        /// Contesto dell'evento
        /// </summary>
        [BsonIgnore]
        public IDictionary<string, object> Context { get; internal set; }

        /// <summary>
        /// This is version of the aggregate, remember that in a single changeset
        /// all the events share the same version after migration to NStore.
        /// </summary>
        [BsonIgnore]
        public Int64 Version { get; internal set; }

        /// <summary>
        /// This is populated by the ICommitEnhancer interface.
        /// </summary>
        [BsonIgnore]
        public Int32 EventPosition { get; private set; }

        /// <summary>
        /// Useful to know if this is the last event projected for a given <see cref="Changeset"/>
        /// </summary>
        [BsonIgnore]
        public Boolean IsLastEventOfCommit { get; set; }

        /// <summary>
        /// With migration to NStore this is the position of the
        /// Chunk in the stream. <see cref="NStore.Core.Persistence.IChunk.Position"/>
        /// </summary>
        [BsonIgnore]
        public Int64 CheckpointToken { get; internal set; }

        /// <summary>
        /// Constructor of the domain event, it automatically generate 
        /// the id of the event. When the event is deserialized, the 
        /// id will be set in corresponding property.
        /// </summary>
        protected DomainEvent()
        {
            this.MessageId = Guid.NewGuid();
        }
    }
}
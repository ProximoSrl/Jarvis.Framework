using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
        /// This is the identification of the users who logically generated the
        /// event, can be overridden with the on-behalf-of attribute.
        /// This property directly accesses dictionary entry on the Context dictionary.
        /// </summary>
        [BsonIgnore]
        public string IssuedBy
        {
            get
            {
                if (this.Context == null)
                {
                    return null;
                }

                object message;
                if (Context.TryGetValue(MessagesConstants.OnBehalfOf, out message))
                {
                    return message as String;
                }

                if (Context.TryGetValue(MessagesConstants.UserId, out message))
                {
                    return message as String;
                }

                return null;
            }
        }

        /// <summary>
        /// Indica il timestamp della commit associata all'evento
        /// </summary>
        [BsonIgnore]
        public DateTime CommitStamp
        {
            get
            {
                return ChangesetCommonHeaders.GetTimestampFromHeaders(Context);
            }
        }

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
        /// This is populated by the ICommitEnhancer interface.
        /// </summary>
        [BsonIgnore]
        public Int32 EventPosition { get; internal set; }

        /// <summary>
        /// Contesto dell'evento
        /// </summary>
        [BsonIgnore]
        public IReadOnlyDictionary<string, object> Context { get; internal set; }

        /// <summary>
        /// Versione dell'aggregato al momento del raise dell'evento
        /// </summary>
        [BsonIgnore]
        public Int64 Version { get; internal set; }

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
﻿using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Events;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    public class UnwindedDomainEvent
    {
        /// <summary>
        /// To signal end of rebuild, this special unwinded domain event is sent to the
        /// TPL queue.
        /// </summary>
        public readonly static UnwindedDomainEvent LastEvent = new UnwindedDomainEvent();

        /// <summary>
        /// Id of the record, is the CheckpointToken concatenated with the ordinal of the event
        /// to generate a unique id that is calculated based on commit data
        /// </summary>
        public String Id { get; set; }

        [BsonElement("e")]
        internal Object Event { get; set; }

        /// <summary>
        /// Type of the event, used to filter events during rebuild.
        /// </summary>
        [BsonElement("t")]
        public String EventType { get; set; }

        [BsonElement("c")]
        public Int64 CheckpointToken { get; set; }

        [BsonElement("s")]
        public Int32 EventSequence { get; set; }

        [BsonElement("ts")]
        public DateTime CommitStamp { get; set; }

        [BsonElement("g")]
        public String CommitId { get; set; }

        [BsonElement("v")]
        public Int64 Version { get; set; }

        [BsonElement("pId")]
        public String PartitionId { get; set; }

        [BsonElement("x")]
        [BsonDictionaryOptions(MongoDB.Bson.Serialization.Options.DictionaryRepresentation.ArrayOfArrays)]
        public IDictionary<String, Object> Context { get; set; }

        private Boolean _enhanced;

        /// <summary>
        /// <see cref="DomainEvent"/> object has a lot of BsonIgnore to avoid serialize unnecessary
        /// properties and uses <see cref="CommitEnhancer" /> to populate these properties.
        /// When the object is unwinded all these properties are in the container object, then
        /// this object uses the same principle of commit ehnancer to set those properties with
        /// FasterFlect
        /// </summary>
        /// <returns></returns>
        public Object GetEvent()
        {
            EnhanceAndUpcastEvent();

            return Event;
        }

        /// <summary>
        /// Enchance and upcast the event.
        /// </summary>
        public void EnhanceAndUpcastEvent()
        {
            if (_enhanced == false && Event is DomainEvent)
            {
                _enhanced = true;
                var domainEvent = Event as DomainEvent;

                domainEvent.CommitId = CommitId;
                domainEvent.Version = Version;
                if (Context != null)
                {
                    domainEvent.Context = new ReadOnlyDictionary<string, object>(Context);
                }
                domainEvent.CheckpointToken = CheckpointToken;
                domainEvent.EventPosition = EventSequence;

                Event = StaticUpcaster.UpcastEvent(domainEvent);
            }
        }
    }
}

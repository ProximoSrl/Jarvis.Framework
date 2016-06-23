using Jarvis.Framework.Shared.Events;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fasterflect;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Bson.Serialization.Attributes;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    public class UnwindedDomainEvent
    {
        /// <summary>
        /// Id of the record, is the CheckpointToken concatenated with the ordinal of the event
        /// to generate a unique id that is calculated based on commit data
        /// </summary>
        public String Id { get; set; }

        [BsonElement("e")]
        internal DomainEvent Event { get; set; }

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
        public Guid CommitId { get; set; }

        [BsonElement("v")]
        public Int32 Version { get; set; }

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
        public DomainEvent GetEvent()
        {
            if (_enhanced == false)
            {
                _enhanced = true;
                Event.SetPropertyValue(d => d.CommitStamp, CommitStamp);
                Event.SetPropertyValue(d => d.CommitId, CommitId);

                Event.SetPropertyValue(d => d.Version, Version);
                Event.SetPropertyValue(d => d.Context, Context);
                Event.SetPropertyValue(d => d.CheckpointToken, CheckpointToken.ToString());
            }

            return Event;
        }
    }
}

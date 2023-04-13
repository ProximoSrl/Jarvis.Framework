using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.ReadModel
{
    public abstract class AbstractReadModel<TKey> :
        IReadModelEx<TKey>,
        IComparable<AbstractReadModel<TKey>>
    {
        private const int CommitIdSkew = 10000;

        public AbstractReadModel()
        {
            ProcessedEvents = new List<Guid>();
        }

        public TKey Id { get; set; }

        /// <inheritdoc />
        public int Version { get; set; }

        /// <inheritdoc />
        public long AggregateVersion { get; set; }

        public DateTime LastModified { get; set; }

        /// <summary>
        /// <para>
        /// We need to keep track of index of last event that
        /// modified this readmodel. We need to store two number
        /// one is the index of the changeset, the other is the
        /// incremental index of the event inside the changeset.
        /// </para>
        /// <para>
        /// To use a single value we simple multiply the index
        /// by CommitIdSkew then add the incremental of the event. This
        /// will work if we have less than CommitIdSkew events in a single
        /// changeset.
        /// </para>
        /// </summary>
        public Int64 LastEventIndexProjected { get; set; }

        /// <summary>
        /// Old logic to understand if an event was already processed, in the new logic after
        /// <see cref="LastEventIndexProjected"/> it will contain only the first event processed.
        /// </summary>
        [JsonIgnore]
        public List<Guid> ProcessedEvents { get; set; }

        /// <summary>
        /// Check if this readmodel was build from this specific event. It will need to use the old collection for backward
        /// compatibility.
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        public bool BuiltFromEvent(DomainEvent evt)
        {
            return BuiltFromMessage(evt.CheckpointToken, evt.EventPosition) || ProcessedEvents?.Contains(evt.MessageId) == true;
        }

        /// <summary>
        /// This version check only a single message id, does not check for
        /// event in the header of the domain event.
        /// </summary>
        /// <param name="commitPosition">This is property CheckpointToken of event</param>
        /// <param name="eventPosition">This is property EventPosition of domainEvent</param>
        /// <returns></returns>
        public Boolean BuiltFromMessage(Int64 commitPosition, Int32 eventPosition)
        {
            return LastEventIndexProjected >= GenerateLastEventIdexProjected(commitPosition, eventPosition);
        }

        public void SetEventProjected(DomainEvent evt)
        {
            if (evt.Version == 0)
            {
                //this is the first event I need to save to avoid creation by two different events
                ProcessedEvents.Add(evt.MessageId);
            }
            LastEventIndexProjected = GenerateLastEventIdexProjected(evt.CheckpointToken, evt.EventPosition);
        }

        private Int64 GenerateLastEventIdexProjected(Int64 commitPosition, Int32 eventPosition)
        {
            return (commitPosition * CommitIdSkew) + eventPosition;
        }

        public void ThrowIfInvalidId()
        {
            if (EqualityComparer<TKey>.Default.Equals(default, Id))
            {
                throw new InvalidAggregateIdException("Empty Id Value");
            }
        }

        public int CompareTo(AbstractReadModel<TKey> other)
        {
            return Comparer<TKey>.Default.Compare(this.Id, other.Id);
        }
    }
}
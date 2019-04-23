using System;
using System.Collections.Generic;
using System.Linq;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Newtonsoft.Json;

namespace Jarvis.Framework.Shared.ReadModel
{
#pragma warning disable S1210 // "Equals" and the comparison operators should be overridden when implementing "IComparable"
	public abstract class AbstractReadModel<TKey> :
#pragma warning restore S1210 // "Equals" and the comparison operators should be overridden when implementing "IComparable"
		IReadModelEx<TKey>,
		IComparable<AbstractReadModel<TKey>>
	{
		public TKey Id { get; set; }
		public int Version { get; set; }
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
        /// by 1000 then add the incremental of the event. This
        /// will work if we have less than 1000 events in a single
        /// changeset.
        /// </para>
        /// </summary>
        public Int64 LastEventIndexProjected { get; set; }

		/// <summary>
		/// Check if this readmodel was build from this specific event.
		/// </summary>
		/// <param name="evt"></param>
		/// <returns></returns>
		public bool BuiltFromEvent(DomainEvent evt)
		{
            //Ouch, we cannot understand if offline engine processed the events.
            return BuiltFromMessage(evt.CheckpointToken, evt.EventPosition);
             
           // || (JarvisFrameworkGlobalConfiguration.OfflineEventsReadmodelIdempotencyCheck && evt.GetOfflineEventIdList().Intersect(ProcessedEvents).Any());
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

		public void SetEventProjected(Int64 commitPosition, Int32 eventPosition)
		{
            LastEventIndexProjected = GenerateLastEventIdexProjected(commitPosition, eventPosition);
        }

        private Int64 GenerateLastEventIdexProjected(Int64 commitPosition, Int32 eventPosition)
        {
            return (commitPosition * 1000) + eventPosition;
        }

        public void ThrowIfInvalidId()
		{
			if (EqualityComparer<TKey>.Default.Equals(default(TKey), Id))
				throw new InvalidAggregateIdException("Empty Id Value");
		}

		public int CompareTo(AbstractReadModel<TKey> other)
		{
			return Comparer<TKey>.Default.Compare(this.Id, other.Id);
		}
	}
}
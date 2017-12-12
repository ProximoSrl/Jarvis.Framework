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
		private const int MaxEventIdToRetain = 30;

		public TKey Id { get; set; }
		public int Version { get; set; }
		public DateTime LastModified { get; set; }

		[JsonIgnore]
		public List<Guid> ProcessedEvents { get; set; }

		protected AbstractReadModel()
		{
			this.ProcessedEvents = new List<Guid>();
		}

		/// <summary>
		/// Need to check both the standard MessageId and also if the readmodel was managed
		/// by one of the id of corresponding offline messages.
		/// </summary>
		/// <param name="evt"></param>
		/// <returns></returns>
		public bool BuiltFromEvent(DomainEvent evt)
		{
			return ProcessedEvents.Contains(evt.MessageId)
				|| evt.GetOfflineEventIdList().Intersect(ProcessedEvents).Any();
		}

		public void AddEvent(Guid id)
		{
			ProcessedEvents.Add(id);
			if (ProcessedEvents.Count > MaxEventIdToRetain)
				ProcessedEvents.Remove(ProcessedEvents[0]);
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
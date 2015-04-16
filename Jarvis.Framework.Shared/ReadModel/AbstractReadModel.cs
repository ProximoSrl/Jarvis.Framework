using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Jarvis.Framework.Shared.ReadModel
{
    public abstract class AbstractReadModel<TKey> : 
        IReadModelEx<TKey>, 
        IComparable<AbstractReadModel<TKey>>
    {
        public TKey Id { get; set; }
        public int Version { get; set; }
        public DateTime LastModified { get; set; }

        [JsonIgnore]
        public List<Guid> ProcessedEvents { get; set; }

        protected AbstractReadModel()
        {
            this.ProcessedEvents = new List<Guid>();
        }

        public bool BuiltFromEvent(Guid id)
        {
            return ProcessedEvents.Contains(id);
        }

        public void AddEvent(Guid id)
        {
            ProcessedEvents.Add(id);
            if (ProcessedEvents.Count > 20)
                ProcessedEvents.Remove(ProcessedEvents[0]);
        }

        public void ThrowIfInvalidId()
        {
            if( EqualityComparer<TKey>.Default.Equals(default (TKey), Id))
                throw new Exception("Empty Id Value");
        }

        public int CompareTo(AbstractReadModel<TKey> other)
        {
            return Comparer<TKey>.Default.Compare(this.Id, other.Id);
        }
    }
}
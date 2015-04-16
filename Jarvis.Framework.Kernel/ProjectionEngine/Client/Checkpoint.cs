using System;
using MongoDB.Bson.Serialization.Attributes;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    [BsonIgnoreExtraElements]
	public class Checkpoint
	{
		public string Id { get; protected set; }
		public string Value { get; set; }
        public string Current { get; set; }
        public string Signature { get; set; }
        public string Slot { get; set; }

	    public DateTime? RebuildStart { get; set; }
	    public DateTime? RebuildStop { get; set; }
        public long RebuildTotalSeconds { get; set; }

        public long Events { get; set; }
        public double RebuildActualSeconds { get; set; }

	    public ProjectionMetrics.Meter Details { get; set; }


        public Checkpoint(string id, string value)
		{
			Id = id;
			Value = value;
		}
	}
}
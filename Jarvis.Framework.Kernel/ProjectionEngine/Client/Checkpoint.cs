using System;
using MongoDB.Bson.Serialization.Attributes;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    [BsonIgnoreExtraElements]
	public class Checkpoint
	{
		public string Id { get; protected set; }
		public Int64 Value { get; set; }
        public Int64? Current { get; set; }
        public string Signature { get; set; }
        public string Slot { get; set; }

        /// <summary>
        /// used to understand if a projection is still active.
        /// </summary>
        public Boolean Active { get; set; }

        public DateTime? RebuildStart { get; set; }
	    public DateTime? RebuildStop { get; set; }
        public long RebuildTotalSeconds { get; set; }

        public long Events { get; set; }
        public double RebuildActualSeconds { get; set; }

	    public ProjectionMetrics.Meter Details { get; set; }


        public Checkpoint(string id, Int64 value, string signature)
		{
			Id = id;
			Value = value;
            Signature = signature;
		}
	}
}
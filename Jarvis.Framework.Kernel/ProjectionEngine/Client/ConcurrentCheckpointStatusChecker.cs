using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public class ConcurrentCheckpointStatusChecker
    {
        private readonly MongoCollection<Checkpoint> _checkpoints;

        public ConcurrentCheckpointStatusChecker(MongoDatabase readmodelDb)
        {
            _checkpoints = readmodelDb.GetCollection<Checkpoint>("checkpoints");
        }

        public bool IsCheckpointProjectedByAllProjection(string checkpointToken)
        {
            return ProjectionsPassedCheckpoint(Convert.ToInt64(checkpointToken));
        }

        private bool ProjectionsPassedCheckpoint(long checkpointToken)
        {
            // Extracts all the projections that have not passed the checkpoint yet.
            var behindProjections = _checkpoints
                .Find(
                    Query.And(
                        Query<Checkpoint>.EQ(x => x.Active, true),
                        Query<Checkpoint>.NE(x => x.Id, "VERSION"),
                        Query.Or(
                            Query.Where("this.Current < " + checkpointToken),
                            Query.EQ("Current", BsonNull.Value)
                        )
                        
                    )
                )
                .SetFields(Fields.Include("_id"));

            return behindProjections.Any() == false;
        }
    }
}

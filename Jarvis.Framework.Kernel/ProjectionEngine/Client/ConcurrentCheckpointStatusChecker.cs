using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public class ConcurrentCheckpointStatusChecker
    {
        private readonly IMongoCollection<Checkpoint> _checkpoints;

        public ConcurrentCheckpointStatusChecker(IMongoDatabase readmodelDb)
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
            var checkpointString = checkpointToken.ToString();
            var behindProjections = _checkpoints
                .Find(
                    Builders< Checkpoint>.Filter.And(
                        Builders<Checkpoint>.Filter.Eq(x => x.Active, true),
                        Builders<Checkpoint>.Filter.Ne(x => x.Id, "VERSION"),
                        Builders<Checkpoint>.Filter.Or(
                            Builders<Checkpoint>.Filter.Lt(x => x.Current, checkpointString),
                            Builders<Checkpoint>.Filter.Eq("Current", BsonNull.Value)
                        )
                        
                    )
                )
                .Project(Builders<Checkpoint>.Projection.Include("_id"));

            return behindProjections.Any() == false;
        }
    }
}

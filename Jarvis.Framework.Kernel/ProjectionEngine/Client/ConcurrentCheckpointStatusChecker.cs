using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Linq;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// This status checker uses direct access to mongo database so it can 
    /// be used not only from projection service, but from all process
    /// that want to check the status of the projection.
    /// </summary>
    public class MongoDirectConcurrentCheckpointStatusChecker : IConcurrentCheckpointStatusChecker
    {
        private readonly IMongoCollection<Checkpoint> _checkpoints;
        private readonly ConcurrentCheckpointTracker _tracker;

        public MongoDirectConcurrentCheckpointStatusChecker(IMongoDatabase readmodelDb, ConcurrentCheckpointTracker tracker)
        {
            _checkpoints = readmodelDb.GetCollection<Checkpoint>("checkpoints");
            _tracker = tracker;
        }

        public Task<bool> IsCheckpointProjectedByAllProjectionAsync(Int64 checkpointToken)
        {
            return ProjectionsPassedCheckpointAsync(checkpointToken);
        }

        private async Task<bool> ProjectionsPassedCheckpointAsync(Int64 checkpointToken)
        {
            // Extracts all the projections that have not passed the checkpoint yet.
            await _tracker.FlushCheckpointCollectionAsync().ConfigureAwait(false);
            var checkpointString = checkpointToken;
            var behindProjections = _checkpoints
                .Find(
                    Builders<Checkpoint>.Filter.And(
                        Builders<Checkpoint>.Filter.Eq(x => x.Active, true),
                        Builders<Checkpoint>.Filter.Ne(x => x.Id, "VERSION"),
                        Builders<Checkpoint>.Filter.Or(
                            Builders<Checkpoint>.Filter.Lt(x => x.Current, checkpointString),
                            Builders<Checkpoint>.Filter.Eq("Current", BsonNull.Value)
                        )
                    )
                )
                .Project(Builders<Checkpoint>.Projection.Include("_id"));

            return !behindProjections.Any();
        }
    }
}

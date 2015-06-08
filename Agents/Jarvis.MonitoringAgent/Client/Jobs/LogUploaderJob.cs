using Castle.Core.Logging;
using Jarvis.MonitoringAgent.Support;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver.Linq;

namespace Jarvis.MonitoringAgent.Client.Jobs
{
    public class LogUploaderJob : IJob
    {
        public ILogger Logger { get; set; }

        public class UploadCheckpoint
        {
            public DateTime LastCheckpoint { get; set; }

            [BsonId]
            public String CollectionName { get; set; }
        }

        public String Connection { get; set; }

        public String Collection { get; set; }


        private MongoCollection<BsonDocument> _collection;
        private MongoCollection<UploadCheckpoint> _checkpointCollection;
        private UploadCheckpoint _checkpoint;

        private class LogCollectionInfo
        {
            private MongoCollection<BsonDocument> _collection;
            private MongoCollection<UploadCheckpoint> _checkpointCollection;
            private UploadCheckpoint _checkpoint;
            public LogCollectionInfo(String connection, String collectionName)
            {
                var url = new MongoUrl(connection);
                var client = new MongoClient(url);
                var db = client.GetServer().GetDatabase(url.DatabaseName);
                _collection = db.GetCollection(collectionName);

                _checkpointCollection = db.GetCollection<UploadCheckpoint>("logUploader.checkpoints");
                _checkpoint = _checkpointCollection.AsQueryable()
                    .SingleOrDefault(d => d.CollectionName == collectionName);
                if (_checkpoint == null)
                {
                    _checkpoint = new UploadCheckpoint() { CollectionName = collectionName, LastCheckpoint = DateTime.MinValue };
                }
            }
        }

        private List<LogCollectionInfo> _logCollectionInfoList = new List<LogCollectionInfo>();

        public LogUploaderJob(MonitoringAgentConfiguration configuration)
        {
            foreach (var logDb in configuration.MongoLogDatabaseList)
            {
                _logCollectionInfoList.Add(new LogCollectionInfo(logDb.ConnectionString, logDb.CollectionName));
            }
        }

        public void Execute(IJobExecutionContext context)
        {
            //start creating zip file to upload.


        }
    }
}

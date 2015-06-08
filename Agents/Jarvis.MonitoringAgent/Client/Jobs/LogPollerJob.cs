using Castle.Core.Logging;
using Jarvis.MonitoringAgent.Support;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Quartz;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver.Linq;
using MongoDB.Driver.Builders;

namespace Jarvis.MonitoringAgent.Client.Jobs
{
    [DisallowConcurrentExecution]
    public class LogPollerJob : IJob
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
        private MonitoringAgentConfiguration _configuration;

        private class LogCollectionInfo
        {
            private MongoCollection<BsonDocument> _collection;
            private MongoCollection<UploadCheckpoint> _checkpointCollection;
            private UploadCheckpoint _checkpoint;
            private String _collectionName;
            private String _connection;

            public string Name { get; private set; }

            public LogCollectionInfo(String connection, String collectionName)
            {
                Name = connection + " Collection " + collectionName;
                _collectionName = collectionName;
                _connection = connection.TrimEnd('/', '\\');

                var url = new MongoUrl(connection);
                var client = new MongoClient(url);
                var db = client.GetServer().GetDatabase(url.DatabaseName);
                _collection = db.GetCollection(collectionName);

                _checkpointCollection = db.GetCollection<UploadCheckpoint>("logUploader.checkpoints");
                _checkpoint = _checkpointCollection.AsQueryable()
                    .SingleOrDefault(d => d.CollectionName == collectionName);
                if (_checkpoint == null)
                {
                    _checkpoint = new UploadCheckpoint() { CollectionName = collectionName, LastCheckpoint = new DateTime(2000, 01, 01) };
                }
            }

            internal List<BsonDocument> GetNextBlockOfLogs()
            {
                DateTime limit = DateTime.Now.AddSeconds(-60); //we tolerate 1 minute time skew
                var logs = _collection.Find(
                    Query.And(
                        Query.GT("ts", _checkpoint.LastCheckpoint),
                        Query.LTE("ts", limit),
                        Query.Or(
                            Query.EQ("le", "ERROR"),
                            Query.EQ("le", "WARN")
                        )
                    )
                )
                .SetSortOrder(SortBy.Ascending("ts"))
                .SetLimit(1000)
                .ToList();

                if (logs.Count > 0)
                {
                    _checkpoint.LastCheckpoint = logs.Last()["ts"].ToUniversalTime();
                    _checkpointCollection.Save(_checkpoint);

                    foreach (var log in logs)
                    {
                        log["source"] = _connection + "/" + _collectionName;
                    }
                }
                return logs;
            }


        }

        private List<LogCollectionInfo> _logCollectionInfoList = new List<LogCollectionInfo>();

        public LogPollerJob(MonitoringAgentConfiguration configuration)
        {
            _configuration = configuration;
            foreach (var logDb in configuration.MongoLogDatabaseList)
            {
                _logCollectionInfoList.Add(new LogCollectionInfo(logDb.ConnectionString, logDb.CollectionName));
            }
        }

        public void Execute(IJobExecutionContext context)
        {
            //start creating zip file to upload.
            foreach (var logInfo in _logCollectionInfoList)
            {
                Logger.DebugFormat("Polling {0}", logInfo.Name);
                List<BsonDocument> logs = logInfo.GetNextBlockOfLogs(); 
                while (logs.Count > 0)
                {
                    Logger.DebugFormat("Found {0} logs with logger {1}", logs.Count, logInfo.Name);

                    //now create a file to upload.
                    String fileName = Path.Combine(
                        _configuration.UploadQueueFolder.FullName, 
                        DateTime.UtcNow.ToString("yyyyMMdd_HHmmffff") + ".logdump"
                    );
                    Logger.DebugFormat("Writing to {0}", fileName);
                    using (FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate))
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        foreach (var log in logs)
                        {
                            var serialized =log.ToJson();
                            sw.WriteLine(serialized);
                        }
                    }
                    logs = logInfo.GetNextBlockOfLogs();
                } ;

            }

        }
    }
}

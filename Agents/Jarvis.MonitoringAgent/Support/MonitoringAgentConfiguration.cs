using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.MonitoringAgent.Support
{
    public class MonitoringAgentConfiguration
    {
        public String CustomerId { get; set; }

        public String PublicEncryptionKey { get; set; }
        
        /// <summary>
        /// this is the list of mongo database with logs that the agent
        /// should send to the server.
        /// </summary>
        public List<MongoLogDatabase> MongoLogDatabaseList { get; set; }

        public DirectoryInfo UploadQueueFolder { get; set; }

        public class MongoLogDatabase
        {
            public String ConnectionString { get; set; }

            public String CollectionName { get; set; }
        }
    }

    public class AppConfigMonitoringAgentConfiguration : MonitoringAgentConfiguration
    {
        public AppConfigMonitoringAgentConfiguration()
        {
            MongoLogDatabaseList = new List<MongoLogDatabase>();
            foreach (var mongoLogSetting in ConfigurationManager.AppSettings
                .AllKeys.Where(k => k.StartsWith("mongo-log")))
            {
                var setting = ConfigurationManager.AppSettings[mongoLogSetting];
                var splittedSetting = setting.Split('|');
                MongoLogDatabaseList.Add(new MongoLogDatabase()
                {
                    ConnectionString = splittedSetting[0],
                    CollectionName = splittedSetting[1],
                });
            }

            UploadQueueFolder = new DirectoryInfo(ConfigurationManager.AppSettings["upload-temp-folder"]);
            if (!UploadQueueFolder.Exists)
            {
                Directory.CreateDirectory(UploadQueueFolder.FullName);
            }
        }
    }
}

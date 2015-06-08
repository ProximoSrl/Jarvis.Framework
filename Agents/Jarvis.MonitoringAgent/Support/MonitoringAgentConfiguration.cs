using Jarvis.MonitoringAgent.Common.Jarvis.MonitoringAgent.Common;
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
        public String CustomerId { get; protected set; }

        public AsymmetricEncryptionKey Key { get; protected set; }

        public String ServerAddress { get; set; }

        /// <summary>
        /// this is the list of mongo database with logs that the agent
        /// should send to the server.
        /// </summary>
        public List<MongoLogDatabase> MongoLogDatabaseList { get; protected set; }

        public DirectoryInfo UploadQueueFolder { get; protected set; }

        public class MongoLogDatabase
        {
            public String ConnectionString { get; internal set; }

            public String CollectionName { get; internal set; }
        }
    }

    public class AppConfigMonitoringAgentConfiguration : MonitoringAgentConfiguration
    {
        public AppConfigMonitoringAgentConfiguration()
        {
            CustomerId = ConfigurationManager.AppSettings["customer-id"];
            var publicKey = ConfigurationManager.AppSettings["customer-key"];
            Key = AsymmetricEncryptionKey.CreateFromString(publicKey, false);

            ServerAddress = ConfigurationManager.AppSettings["server-address"].TrimEnd('/', '\\');

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
